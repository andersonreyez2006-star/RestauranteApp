using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;
namespace RestauranteApp.Controllers;

// Permite a administrador y cajero consultar comprobantes y cerrar órdenes mediante el cobro.
[Authorize(Roles = "Administrador,Cajero")]
public class FacturasController(AppDbContext db) : Controller
{
    // Tasa decimal 0.13 aplicada al subtotal para mostrar IVA del 13 %.
    private const decimal TasaIva = 0.13m; // IVA El Salvador

    // Lista facturas recientes con la orden y mesa necesarias para las columnas del resumen.
    public async Task<IActionResult> Index() => View(await db.Facturas.Include(f => f.Orden).ThenInclude(o => o!.Mesa).OrderByDescending(f => f.Fecha).ToListAsync());

    // Carga las líneas, mesa y pagos para componer el comprobante completo.
    public async Task<IActionResult> Details(int id)
    {
        var f = await db.Facturas.Include(x => x.Pagos).Include(x => x.Orden).ThenInclude(o => o!.Mesa)
            .Include(x => x.Orden).ThenInclude(o => o!.Detalles).ThenInclude(d => d.Platillo).FirstOrDefaultAsync(x => x.IdFactura == id);
        return f == null ? NotFound() : View(f);
    }

    // Emite una única factura para una orden con líneas y aún no facturada.
    // El método de pago procede del formulario, pero la orden y sus importes siempre se vuelven a leer desde SQL.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(int idOrden, string metodoPago)
    {
        var orden = await db.Ordenes.Include(x => x.Detalles).Include(x => x.Mesa).FirstOrDefaultAsync(x => x.IdOrden == idOrden);
        if (orden == null) return NotFound();
        if (orden.Estado == "Facturada" || !orden.Detalles.Any())
        { TempData["Error"] = "La orden no se puede facturar."; return RedirectToAction("Details", "Ordenes", new { id = idOrden }); }
        // Se calcula el IVA sobre la suma de los subtotales y se redondea a centavos.
        var sub = orden.Detalles.Sum(d => d.Subtotal); var iva = Math.Round(sub * TasaIva, 2);
        var f = new Factura { IdOrden = idOrden, Subtotal = sub, IVA = iva, Total = sub + iva };
        f.Pagos.Add(new Pago { MetodoPago = string.IsNullOrWhiteSpace(metodoPago) ? "Efectivo" : metodoPago, Monto = f.Total });
        // La orden y la mesa se actualizan en el mismo SaveChanges que persiste factura y pago,
        // evitando que la mesa quede ocupada después de completar el cobro correctamente.
        orden.Estado = "Facturada"; if (orden.Mesa != null) orden.Mesa.Estado = "Libre";
        db.Facturas.Add(f); await db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = f.IdFactura });
    }
}

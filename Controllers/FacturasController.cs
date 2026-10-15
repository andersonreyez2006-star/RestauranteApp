using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;
namespace RestauranteApp.Controllers;

// Consulta y genera facturas para las órdenes procesadas por administrador o cajero.
[Authorize(Roles = "Administrador,Cajero")]
public class FacturasController(AppDbContext db) : Controller
{
    private const decimal TasaIva = 0.13m; // IVA El Salvador

    public async Task<IActionResult> Index() => View(await db.Facturas.Include(f => f.Orden).ThenInclude(o => o!.Mesa).OrderByDescending(f => f.Fecha).ToListAsync());

    public async Task<IActionResult> Details(int id)
    {
        var f = await db.Facturas.Include(x => x.Pagos).Include(x => x.Orden).ThenInclude(o => o!.Mesa)
            .Include(x => x.Orden).ThenInclude(o => o!.Detalles).ThenInclude(d => d.Platillo).FirstOrDefaultAsync(x => x.IdFactura == id);
        return f == null ? NotFound() : View(f);
    }

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
        // El cobro cierra la orden y libera la mesa en la misma operación de base de datos.
        orden.Estado = "Facturada"; if (orden.Mesa != null) orden.Mesa.Estado = "Libre";
        db.Facturas.Add(f); await db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = f.IdFactura });
    }
}

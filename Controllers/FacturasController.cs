using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;
namespace RestauranteApp.Controllers;

// Permite a administrador y cajero consultar comprobantes y cerrar órdenes mediante el cobro.
[Authorize(Policy = "BillingAccess")]
public class FacturasController(AppDbContext db) : Controller
{
    // Tasa decimal 0.13 aplicada al subtotal para mostrar IVA del 13 %.
    private const decimal TasaIva = 0.13m; // IVA El Salvador
    private static readonly string[] MetodosPago = ["Efectivo", "Tarjeta", "Transferencia"];

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
        var metodoValidado = MetodosPago.FirstOrDefault(m =>
            string.Equals(m, metodoPago?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (metodoValidado is null)
        {
            TempData["Error"] = "Selecciona un método de pago válido.";
            return RedirectToAction("Details", "Ordenes", new { id = idOrden });
        }

        var facturaExistente = await db.Facturas.AsNoTracking().FirstOrDefaultAsync(f => f.IdOrden == idOrden);
        if (facturaExistente is not null)
        {
            TempData["Error"] = "Esta orden ya tiene una factura.";
            return RedirectToAction(nameof(Details), new { id = facturaExistente.IdFactura });
        }

        var orden = await db.Ordenes.Include(x => x.Detalles).Include(x => x.Mesa).FirstOrDefaultAsync(x => x.IdOrden == idOrden);
        if (orden == null) return NotFound();
        if (orden.Estado == "Facturada" || !orden.Detalles.Any())
        { TempData["Error"] = "La orden no se puede facturar."; return RedirectToAction("Details", "Ordenes", new { id = idOrden }); }
        // Se calcula el IVA sobre la suma de los subtotales y se redondea a centavos.
        var sub = orden.Detalles.Sum(d => d.Subtotal); var iva = Math.Round(sub * TasaIva, 2);
        var f = new Factura { IdOrden = idOrden, Subtotal = sub, IVA = iva, Total = sub + iva };
        f.Pagos.Add(new Pago { MetodoPago = metodoValidado, Monto = f.Total });
        // La mesa solo se libera cuando ya no tiene otras órdenes pendientes de facturación.
        orden.Estado = "Facturada";
        orden.TokenPedidoPublico = null;
        if (orden.Mesa != null)
        {
            var otrasOrdenesAbiertas = await db.Ordenes.AnyAsync(o =>
                o.IdMesa == orden.IdMesa && o.IdOrden != idOrden && o.Estado != "Facturada");
            if (!otrasOrdenesAbiertas)
                orden.Mesa.Estado = "Libre";
        }
        db.Facturas.Add(f);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // El índice único también protege contra dos cobros simultáneos de la misma orden.
            facturaExistente = await db.Facturas.AsNoTracking().FirstOrDefaultAsync(x => x.IdOrden == idOrden);
            if (facturaExistente is null) throw;

            TempData["Error"] = "Esta orden ya fue facturada.";
            return RedirectToAction(nameof(Details), new { id = facturaExistente.IdFactura });
        }

        return RedirectToAction(nameof(Details), new { id = f.IdFactura });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;

namespace RestauranteApp.Controllers;

[Authorize(Roles = "Administrador,Cajero")]
public class ReportesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(DateTime? fecha)
    {
        var dia = (fecha ?? DateTime.Today).Date;
        var hasta = dia.AddDays(1);
        var facturas = db.Facturas.Where(f => f.Fecha >= dia && f.Fecha < hasta);
        var detalles = await db.Detalles.Include(d => d.Platillo).Include(d => d.Orden)
            .Where(d => d.Orden != null && d.Orden.Estado == "Facturada" && d.Orden.Fecha >= dia && d.Orden.Fecha < hasta)
            .GroupBy(d => d.Platillo!.Nombre).Select(g => new { Nombre = g.Key, Unidades = g.Sum(x => x.Cantidad) })
            .OrderByDescending(x => x.Unidades).Take(5).ToListAsync();
        var vm = new ReporteVentasViewModel
        {
            Fecha = dia,
            CantidadFacturas = await facturas.CountAsync(),
            TotalVentas = await facturas.SumAsync(f => (decimal?)f.Total) ?? 0,
            MasVendidos = detalles.Select(x => (x.Nombre, x.Unidades)).ToList(),
            BajoStock = await db.Platillos.Where(p => p.Existencias <= 5).OrderBy(p => p.Existencias).ToListAsync()
        };
        return View(vm);
    }
}

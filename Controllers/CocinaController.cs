using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
namespace RestauranteApp.Controllers;

[Authorize(Roles = "Administrador,Cocina")]
public class CocinaController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() =>
        View(await db.Ordenes.Include(x => x.Mesa).Include(x => x.Detalles).ThenInclude(d => d.Platillo)
            .Where(x => (x.Estado == "Pendiente" || x.Estado == "En preparación") && x.Detalles.Any())
            .OrderBy(x => x.Fecha).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Avanzar(int id)
    {
        var o = await db.Ordenes.FindAsync(id); if (o == null) return NotFound();
        o.Estado = o.Estado == "Pendiente" ? "En preparación" : o.Estado == "En preparación" ? "Lista" : o.Estado;
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }
}

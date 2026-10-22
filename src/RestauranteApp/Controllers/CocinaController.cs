using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
namespace RestauranteApp.Controllers;

// Limita el tablero y las transiciones de preparación a cocina y administración.
[Authorize(Roles = "Administrador,Cocina")]
public class CocinaController(AppDbContext db) : Controller
{
    // Incluye mesa, líneas y nombres de platillos para que la vista renderice la comanda sin consultas por fila.
    // Solo muestra órdenes con productos que aún no están listas ni facturadas, de la más antigua a la nueva.
    public async Task<IActionResult> Index() =>
        View(await db.Ordenes.Include(x => x.Mesa).Include(x => x.Detalles).ThenInclude(d => d.Platillo)
            .Where(x => (x.Estado == "Pendiente" || x.Estado == "En preparación") && x.Detalles.Any())
            .OrderBy(x => x.Fecha).ToListAsync());

    // Avanza Pendiente → En preparación → Lista; otros estados se conservan sin cambio.
    // Se usa POST con token antifalsificación porque esta acción modifica el estado persistido.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Avanzar(int id)
    {
        var o = await db.Ordenes.FindAsync(id); if (o == null) return NotFound();
        o.Estado = o.Estado == "Pendiente" ? "En preparación" : o.Estado == "En preparación" ? "Lista" : o.Estado;
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }
}

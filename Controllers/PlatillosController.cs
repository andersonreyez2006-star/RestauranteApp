using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;
namespace RestauranteApp.Controllers;

[Authorize(Roles = "Administrador")]
public class PlatillosController(AppDbContext db) : Controller
{
    private void Cargar(int? sel = null) => ViewBag.Categorias = new SelectList(db.Categorias.OrderBy(c => c.Nombre), "IdCategoria", "Nombre", sel);

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        IQueryable<Platillo> menu = db.Platillos.Include(p => p.Categoria);
        if (!User.IsInRole("Administrador")) menu = menu.Where(p => p.Estado == "Disponible" && p.Existencias > 0);
        return View(await menu.OrderBy(p => p.Nombre).ToListAsync());
    }

    public IActionResult Create() { Cargar(); return View("Form", new Platillo()); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Platillo p)
    {
        if (!ModelState.IsValid) { Cargar(p.IdCategoria); return View("Form", p); }
        db.Platillos.Add(p); await db.SaveChangesAsync();
        TempData["Ok"] = "Platillo creado."; return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var p = await db.Platillos.FindAsync(id); if (p == null) return NotFound();
        Cargar(p.IdCategoria); return View("Form", p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Platillo p)
    {
        if (id != p.IdPlatillo) return BadRequest();
        if (!ModelState.IsValid) { Cargar(p.IdCategoria); return View("Form", p); }
        db.Update(p); await db.SaveChangesAsync();
        TempData["Ok"] = "Platillo actualizado."; return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await db.Platillos.FindAsync(id); if (p == null) return NotFound();
        try { db.Platillos.Remove(p); await db.SaveChangesAsync(); TempData["Ok"] = "Platillo eliminado."; }
        catch (DbUpdateException) { TempData["Error"] = "No se puede eliminar: el platillo tiene órdenes asociadas."; }
        return RedirectToAction(nameof(Index));
    }
}

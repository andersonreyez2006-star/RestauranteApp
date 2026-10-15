using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;
namespace RestauranteApp.Controllers;

// Administra mesas y evita duplicar números o borrar mesas con órdenes relacionadas.
[Authorize(Roles = "Administrador")]
public class MesasController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Mesas.OrderBy(m => m.NumeroMesa).ToListAsync());
    public IActionResult Create() => View("Form", new Mesa());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Mesa m)
    {
        // El número debe ser único antes de guardar una mesa nueva.
        if (await db.Mesas.AnyAsync(x => x.NumeroMesa == m.NumeroMesa)) ModelState.AddModelError(nameof(m.NumeroMesa), "Ya existe una mesa con ese número.");
        if (!ModelState.IsValid) return View("Form", m);
        db.Mesas.Add(m); await db.SaveChangesAsync(); TempData["Ok"] = "Mesa creada."; return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id) { var m = await db.Mesas.FindAsync(id); return m == null ? NotFound() : View("Form", m); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Mesa m)
    {
        if (id != m.IdMesa) return BadRequest();
        // Excluye el registro actual para permitir guardar su propio número sin falso duplicado.
        if (await db.Mesas.AnyAsync(x => x.NumeroMesa == m.NumeroMesa && x.IdMesa != id)) ModelState.AddModelError(nameof(m.NumeroMesa), "Ya existe una mesa con ese número.");
        if (!ModelState.IsValid) return View("Form", m);
        db.Update(m); await db.SaveChangesAsync(); TempData["Ok"] = "Mesa actualizada."; return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var m = await db.Mesas.FindAsync(id); if (m == null) return NotFound();
        try { db.Mesas.Remove(m); await db.SaveChangesAsync(); TempData["Ok"] = "Mesa eliminada."; }
        catch (DbUpdateException) { TempData["Error"] = "No se puede eliminar: la mesa tiene órdenes asociadas."; }
        return RedirectToAction(nameof(Index));
    }
}

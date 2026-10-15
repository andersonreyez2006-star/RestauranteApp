using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;

namespace RestauranteApp.Controllers;

// Mantiene las categorías del menú; todas las operaciones requieren rol de administrador.
[Authorize(Roles = "Administrador")]
public class CategoriasController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Categorias.Include(c => c.Platillos).OrderBy(c => c.Nombre).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string nombre)
    {
        nombre = (nombre ?? "").Trim();
        // La normalización evita nombres vacíos y duplicados que solo difieran en mayúsculas.
        if (nombre.Length is < 2 or > 100) TempData["Error"] = "El nombre debe tener entre 2 y 100 caracteres.";
        else if (await db.Categorias.AnyAsync(c => c.Nombre.ToLower() == nombre.ToLower())) TempData["Error"] = "Ya existe esa categoría.";
        else { db.Categorias.Add(new Categoria { Nombre = nombre }); await db.SaveChangesAsync(); TempData["Ok"] = "Categoría creada."; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(int id, string nombre)
    {
        var c = await db.Categorias.FindAsync(id); if (c == null) return NotFound();
        nombre = (nombre ?? "").Trim();
        if (nombre.Length is < 2 or > 100) TempData["Error"] = "El nombre debe tener entre 2 y 100 caracteres.";
        else { c.Nombre = nombre; await db.SaveChangesAsync(); TempData["Ok"] = "Categoría actualizada."; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await db.Categorias.Include(x => x.Platillos).FirstOrDefaultAsync(x => x.IdCategoria == id); if (c == null) return NotFound();
        // Se evita dejar platillos con una categoría inexistente.
        if (c.Platillos.Count != 0) TempData["Error"] = "No se puede eliminar: primero reasigna o elimina sus platillos.";
        else { db.Categorias.Remove(c); await db.SaveChangesAsync(); TempData["Ok"] = "Categoría eliminada."; }
        return RedirectToAction(nameof(Index));
    }
}

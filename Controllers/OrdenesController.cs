using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;
namespace RestauranteApp.Controllers;

[Authorize(Roles = "Administrador,Mesero,Cajero")]
public class OrdenesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() =>
        View(await db.Ordenes.Include(x => x.Mesa).Include(x => x.Usuario).Include(x => x.Detalles).OrderByDescending(x => x.Fecha).ToListAsync());

    [Authorize(Roles = "Administrador,Mesero")]
    public IActionResult Create()
    {
        ViewBag.Mesas = new SelectList(db.Mesas.Where(m => m.Estado == "Libre").OrderBy(m => m.NumeroMesa), "IdMesa", "NumeroMesa");
        return View();
    }

    [AllowAnonymous]
    public async Task<IActionResult> CreatePublic()
    {
        ViewBag.Mesas = await db.Mesas.Where(m => m.Estado == "Libre").OrderBy(m => m.NumeroMesa).ToListAsync();
        ViewBag.Platillos = await db.Platillos.Include(p => p.Categoria).Where(p => p.Estado == "Disponible" && p.Existencias > 0).OrderBy(p => p.Categoria!.Nombre).ThenBy(p => p.Nombre).ToListAsync();
        return View();
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePublic(int idMesa, Dictionary<int, int> cantidades)
    {
        var mesa = await db.Mesas.FindAsync(idMesa);
        if (mesa == null || mesa.Estado != "Libre") { TempData["Error"] = "La mesa no está disponible."; return RedirectToAction(nameof(CreatePublic)); }
        var pedido = cantidades.Where(x => x.Value > 0).ToList();
        if (pedido.Count == 0) { TempData["Error"] = "Selecciona al menos un platillo."; return RedirectToAction(nameof(CreatePublic)); }
        var ids = pedido.Select(x => x.Key).ToList();
        var platos = await db.Platillos.Where(p => ids.Contains(p.IdPlatillo) && p.Estado == "Disponible").ToListAsync();
        if (platos.Count != pedido.Count || pedido.Any(x => x.Value > 20 || platos.Single(p => p.IdPlatillo == x.Key).Existencias < x.Value))
        { TempData["Error"] = "Revisa las cantidades disponibles del menú."; return RedirectToAction(nameof(CreatePublic)); }
        var orden = new Orden { IdMesa = idMesa, Estado = "Pendiente" };
        foreach (var item in pedido)
        {
            var plato = platos.Single(p => p.IdPlatillo == item.Key);
            plato.Existencias -= item.Value;
            if (plato.Existencias == 0) plato.Estado = "Agotado";
            orden.Detalles.Add(new DetalleOrden { IdPlatillo = plato.IdPlatillo, Cantidad = item.Value, PrecioUnitario = plato.Precio, Subtotal = plato.Precio * item.Value });
        }
        mesa.Estado = "Ocupada";
        db.Ordenes.Add(orden);
        await db.SaveChangesAsync();
        TempData["Ok"] = $"Pedido #{orden.IdOrden} enviado a cocina.";
        return RedirectToAction(nameof(CreatePublic));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrador,Mesero")]
    public async Task<IActionResult> Create(int idMesa)
    {
        var mesa = await db.Mesas.FindAsync(idMesa);
        if (mesa == null || mesa.Estado != "Libre") { TempData["Error"] = "La mesa no está disponible."; return RedirectToAction(nameof(Create)); }
        var orden = new Orden { IdMesa = idMesa, IdUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!) };
        mesa.Estado = "Ocupada"; db.Ordenes.Add(orden); await db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = orden.IdOrden });
    }

    public async Task<IActionResult> Details(int id)
    {
        var orden = await db.Ordenes.Include(x => x.Mesa).Include(x => x.Detalles).ThenInclude(d => d.Platillo).FirstOrDefaultAsync(x => x.IdOrden == id);
        if (orden == null) return NotFound();
        ViewBag.Platillos = new SelectList(db.Platillos.Where(p => p.Estado == "Disponible").OrderBy(p => p.Nombre), "IdPlatillo", "Nombre");
        return View(orden);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrador,Mesero")]
    public async Task<IActionResult> AddItem(int id, int idPlatillo, int cantidad)
    {
        var orden = await db.Ordenes.FindAsync(id); var plato = await db.Platillos.FindAsync(idPlatillo);
        if (orden == null || plato == null) return NotFound();
        if (orden.Estado == "Facturada" || cantidad < 1 || plato.Estado != "Disponible")
        { TempData["Error"] = "No se pudo agregar el platillo."; return RedirectToAction(nameof(Details), new { id }); }
        if (cantidad > plato.Existencias) { TempData["Error"] = "No hay suficientes existencias."; return RedirectToAction(nameof(Details), new { id }); }
        plato.Existencias -= cantidad;
        if (plato.Existencias == 0) plato.Estado = "Agotado";
        db.Detalles.Add(new DetalleOrden { IdOrden = id, IdPlatillo = idPlatillo, Cantidad = cantidad, PrecioUnitario = plato.Precio, Subtotal = plato.Precio * cantidad });
        if (orden.Estado == "Lista") orden.Estado = "Pendiente";
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrador,Mesero")]
    public async Task<IActionResult> RemoveItem(int idDetalle)
    {
        var d = await db.Detalles.Include(x => x.Orden).FirstOrDefaultAsync(x => x.IdDetalle == idDetalle);
        if (d == null) return NotFound();
        var id = d.IdOrden;
        if (d.Orden!.Estado != "Facturada") { var p = await db.Platillos.FindAsync(d.IdPlatillo); if (p != null) { p.Existencias += d.Cantidad; p.Estado = "Disponible"; } db.Detalles.Remove(d); await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Details), new { id });
    }
}

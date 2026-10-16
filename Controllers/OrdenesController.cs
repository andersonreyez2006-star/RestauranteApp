using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;
namespace RestauranteApp.Controllers;

// Centraliza el ciclo de pedidos para personal y clientes. Las cantidades, precios, estados de mesa
// e inventario se vuelven a comprobar en el servidor antes de persistir cualquier cambio.
[Authorize(Roles = "Administrador,Mesero,Cajero")]
public class OrdenesController(AppDbContext db) : Controller
{
    private const string PedidoPublicoCookie = "Restaurante.PedidoPublico";

    // Resumen administrativo: incluye navegación a mesa, usuario y detalles para evitar lecturas por fila en la vista.
    public async Task<IActionResult> Index() =>
        View(await db.Ordenes.Include(x => x.Mesa).Include(x => x.Usuario).Include(x => x.Detalles).OrderByDescending(x => x.Fecha).ToListAsync());

    [Authorize(Roles = "Administrador,Mesero")]
    // Formulario interno para elegir una mesa libre; la acción que guarda vuelve a comprobar el estado.
    public IActionResult Create()
    {
        ViewBag.Mesas = new SelectList(db.Mesas.Where(m => m.Estado == "Libre").OrderBy(m => m.NumeroMesa), "IdMesa", "NumeroMesa");
        return View();
    }

    [AllowAnonymous]
    // Catálogo público de pedido. Solo expone mesas libres y productos disponibles con stock positivo.
    public async Task<IActionResult> CreatePublic()
    {
        var mesas = await db.Mesas.Where(m => m.Estado == "Libre").ToListAsync();
        var pedidoActivo = await GetPedidoPublicoActivoAsync();
        if (pedidoActivo.Mesa is not null && mesas.All(m => m.IdMesa != pedidoActivo.Mesa.IdMesa))
            mesas.Add(pedidoActivo.Mesa);

        ViewBag.Mesas = mesas.OrderBy(m => m.NumeroMesa).ToList();
        ViewBag.MesaSeleccionada = pedidoActivo.Mesa?.IdMesa;
        ViewBag.NumeroPedidoEnviado = TempData["NumeroPedidoEnviado"];
        ViewBag.NumeroMesaPedidoEnviado = TempData["NumeroMesaPedidoEnviado"];
        ViewBag.Platillos = await db.Platillos.Include(p => p.Categoria).Where(p => p.Estado == "Disponible" && p.Existencias > 0).OrderBy(p => p.Categoria!.Nombre).ThenBy(p => p.Nombre).ToListAsync();
        return View();
    }

    // Acepta un diccionario de idPlatillo→cantidad. Al ser anónimo, no confía en ningún precio,
    // nombre, estado o stock enviado por el navegador: consulta las entidades y valida todo de nuevo.
    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePublic(int idMesa, Dictionary<int, int> cantidades)
    {
        var mesa = await db.Mesas.FindAsync(idMesa);
        var pedidoActivo = await GetPedidoPublicoActivoAsync();
        var puedeContinuarEnMesa = pedidoActivo.Mesa?.IdMesa == idMesa;
        if (mesa == null || (mesa.Estado != "Libre" && !puedeContinuarEnMesa))
        { TempData["Error"] = "La mesa no está disponible para este pedido."; return RedirectToAction(nameof(CreatePublic)); }
        var pedido = cantidades.Where(x => x.Value > 0).ToList();
        if (pedido.Count == 0) { TempData["Error"] = "Selecciona al menos un platillo."; return RedirectToAction(nameof(CreatePublic)); }
        var ids = pedido.Select(x => x.Key).ToList();
        var platos = await db.Platillos.Where(p => ids.Contains(p.IdPlatillo) && p.Estado == "Disponible").ToListAsync();
        // Rechaza ids que ya no están disponibles, cantidades mayores que el límite del formulario
        // o stock insuficiente; esta validación evita pedidos manipulados desde el navegador.
        if (platos.Count != pedido.Count || pedido.Any(x => x.Value > 20 || platos.Single(p => p.IdPlatillo == x.Key).Existencias < x.Value))
        { TempData["Error"] = "Revisa las cantidades disponibles del menú."; return RedirectToAction(nameof(CreatePublic)); }
        var orden = new Orden { IdMesa = idMesa, Estado = "Pendiente" };
        foreach (var item in pedido)
        {
            var plato = platos.Single(p => p.IdPlatillo == item.Key);
            // Guarda precio y subtotal en la línea como valores históricos; futuras ediciones de Platillo
            // no deben cambiar lo que el cliente pidió ni el importe que se facturará.
            plato.Existencias -= item.Value;
            if (plato.Existencias == 0) plato.Estado = "Agotado";
            orden.Detalles.Add(new DetalleOrden { IdPlatillo = plato.IdPlatillo, Cantidad = item.Value, PrecioUnitario = plato.Precio, Subtotal = plato.Precio * item.Value });
        }
        mesa.Estado = "Ocupada";
        if (puedeContinuarEnMesa && pedidoActivo.Orden is not null)
            pedidoActivo.Orden.TokenPedidoPublico = null;
        var tokenPedidoPublico = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        orden.TokenPedidoPublico = HashTokenPedidoPublico(tokenPedidoPublico);
        db.Ordenes.Add(orden);
        await db.SaveChangesAsync();
        Response.Cookies.Append(PedidoPublicoCookie, tokenPedidoPublico,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromHours(8),
                Path = Url.Action(nameof(CreatePublic), "Ordenes") ?? "/Ordenes/CreatePublic"
            });
        TempData["NumeroPedidoEnviado"] = orden.IdOrden;
        TempData["NumeroMesaPedidoEnviado"] = mesa.NumeroMesa;
        TempData["Ok"] = $"Pedido #{orden.IdOrden} enviado a cocina. Puedes hacer otro pedido para la misma mesa mientras siga abierta.";
        return RedirectToAction(nameof(CreatePublic));
    }

    private async Task<(Orden? Orden, Mesa? Mesa)> GetPedidoPublicoActivoAsync()
    {
        if (!Request.Cookies.TryGetValue(PedidoPublicoCookie, out var token) || string.IsNullOrWhiteSpace(token))
            return (null, null);

        if (token.Length != 64 || token.Any(c => !Uri.IsHexDigit(c)))
            return (null, null);

        var tokenHash = HashTokenPedidoPublico(token);
        var orden = await db.Ordenes
            .FirstOrDefaultAsync(o => o.TokenPedidoPublico == tokenHash && o.Estado != "Facturada");
        if (orden is null)
            return (null, null);

        var mesa = await db.Mesas.AsNoTracking()
            .FirstOrDefaultAsync(m => m.IdMesa == orden.IdMesa && m.Estado == "Ocupada");
        return (mesa is null ? null : orden, mesa);
    }

    private static string HashTokenPedidoPublico(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrador,Mesero")]
    // Abre una orden vacía desde el flujo del personal y registra quién la creó en IdUsuario.
    public async Task<IActionResult> Create(int idMesa)
    {
        var mesa = await db.Mesas.FindAsync(idMesa);
        if (mesa == null || mesa.Estado != "Libre") { TempData["Error"] = "La mesa no está disponible."; return RedirectToAction(nameof(Create)); }
        var orden = new Orden { IdMesa = idMesa, IdUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!) };
        mesa.Estado = "Ocupada"; db.Ordenes.Add(orden); await db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = orden.IdOrden });
    }

    // Recupera todos los datos que necesita la vista para mostrar líneas y agregar productos.
    public async Task<IActionResult> Details(int id)
    {
        var orden = await db.Ordenes.Include(x => x.Mesa).Include(x => x.Detalles).ThenInclude(d => d.Platillo).FirstOrDefaultAsync(x => x.IdOrden == id);
        if (orden == null) return NotFound();
        ViewBag.FacturaExistenteId = await db.Facturas.AsNoTracking()
            .Where(f => f.IdOrden == id)
            .Select(f => (int?)f.IdFactura)
            .FirstOrDefaultAsync();
        ViewBag.Platillos = new SelectList(db.Platillos.Where(p => p.Estado == "Disponible").OrderBy(p => p.Nombre), "IdPlatillo", "Nombre");
        return View(orden);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrador,Mesero")]
    // Agrega una línea solo si la orden sigue abierta y el producto está disponible con stock suficiente.
    public async Task<IActionResult> AddItem(int id, int idPlatillo, int cantidad)
    {
        var orden = await db.Ordenes.FindAsync(id); var plato = await db.Platillos.FindAsync(idPlatillo);
        if (orden == null || plato == null) return NotFound();
        var facturaExiste = await db.Facturas.AnyAsync(f => f.IdOrden == id);
        if (facturaExiste || orden.Estado == "Facturada" || cantidad < 1 || plato.Estado != "Disponible")
        { TempData["Error"] = "No se pudo agregar el platillo."; return RedirectToAction(nameof(Details), new { id }); }
        if (cantidad > plato.Existencias) { TempData["Error"] = "No hay suficientes existencias."; return RedirectToAction(nameof(Details), new { id }); }
        // El descuento y la línea se guardan juntos. Si se agrega algo después de marcar como Lista,
        // la orden vuelve a Pendiente para que cocina procese el nuevo producto.
        plato.Existencias -= cantidad;
        if (plato.Existencias == 0) plato.Estado = "Agotado";
        db.Detalles.Add(new DetalleOrden { IdOrden = id, IdPlatillo = idPlatillo, Cantidad = cantidad, PrecioUnitario = plato.Precio, Subtotal = plato.Precio * cantidad });
        if (orden.Estado == "Lista") orden.Estado = "Pendiente";
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrador,Mesero")]
    // Retira la línea solo antes de facturar y reintegra sus unidades al inventario.
    public async Task<IActionResult> RemoveItem(int idDetalle)
    {
        var d = await db.Detalles.Include(x => x.Orden).FirstOrDefaultAsync(x => x.IdDetalle == idDetalle);
        if (d == null) return NotFound();
        var id = d.IdOrden;
        var facturaExiste = await db.Facturas.AnyAsync(f => f.IdOrden == id);
        if (facturaExiste)
        {
            TempData["Error"] = "No se pueden modificar los artículos de una orden ya facturada.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // No se permite modificar una orden cerrada; en una abierta se devuelve el stock y se elimina la línea.
        if (d.Orden!.Estado != "Facturada") { var p = await db.Platillos.FindAsync(d.IdPlatillo); if (p != null) { p.Existencias += d.Cantidad; p.Estado = "Disponible"; } db.Detalles.Remove(d); await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Details), new { id });
    }
}

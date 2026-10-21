using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;
namespace RestauranteApp.Controllers;

// El controlador requiere administrador por defecto; Index e Imagen son las únicas lecturas públicas.
// La carga acepta archivos limitados y validados, y guarda sus bytes en la misma base que el catálogo.
[Authorize(Roles = "Administrador")]
public class PlatillosController(AppDbContext db) : Controller
{
    // Mantiene margen para encabezados multipart además del máximo de 5 MiB del contenido de imagen.
    private const long MaxImageSize = 5 * 1024 * 1024;

    // Prepara el selector de categorías conservando la selección al volver a mostrar un formulario inválido.
    private void Cargar(int? sel = null) => ViewBag.Categorias = new SelectList(db.Categorias.OrderBy(c => c.Nombre), "IdCategoria", "Nombre", sel);

    // Entrega el archivo binario para los <img> del menú; no almacena copias temporales ni permite caché obsoleta.
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Imagen(int id)
    {
        // Las imágenes se leen desde SQL para que no dependan del almacenamiento efímero del contenedor.
        var image = await db.Platillos.AsNoTracking()
            .Where(p => p.IdPlatillo == id)
            .Select(p => new { p.Imagen, p.ImagenContentType })
            .FirstOrDefaultAsync();

        return image?.Imagen is { Length: > 0 } bytes && image.ImagenContentType is not null
            ? File(bytes, image.ImagenContentType)
            : NotFound();
    }

    [AllowAnonymous]
    // Los clientes ven solo productos vendibles; el administrador ve también agotados para poder editarlos.
    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Cocina"))
            return RedirectToAction("Index", "Cocina");

        IQueryable<Platillo> menu = db.Platillos.Include(p => p.Categoria);
        // El público solo ve artículos vendibles; administración también necesita ver agotados.
        if (!User.IsInRole("Administrador")) menu = menu.Where(p => p.Estado == "Disponible" && p.Existencias > 0 && p.Precio > 0);
        return View(await menu.OrderBy(p => p.Categoria!.Nombre).ThenBy(p => p.Nombre).ToListAsync());
    }

    // Inicializa el formulario y su lista de categorías para crear un producto.
    public IActionResult Create() { Cargar(); return View("Form", new Platillo()); }

    // Recibe campos del platillo y un archivo opcional. El límite de solicitud evita aceptar cuerpos grandes
    // antes de que la validación individual del archivo compruebe tamaño y firma real.
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(MaxImageSize + 64 * 1024)]
    public async Task<IActionResult> Create(Platillo p, IFormFile? imagen)
    {
        var uploadedImage = await LeerImagenAsync(imagen);
        if (imagen is not null && uploadedImage is null)
        {
            Cargar(p.IdCategoria);
            return View("Form", p);
        }

        if (!ModelState.IsValid) { Cargar(p.IdCategoria); return View("Form", p); }
        if (uploadedImage is not null)
        {
            p.Imagen = uploadedImage.Value.Contenido;
            p.ImagenContentType = uploadedImage.Value.Tipo;
        }
        db.Platillos.Add(p); await db.SaveChangesAsync();
        TempData["Ok"] = "Platillo creado."; return RedirectToAction(nameof(Index));
    }

    // Carga los datos actuales para que el formulario pueda conservar y previsualizar la imagen existente.
    public async Task<IActionResult> Edit(int id)
    {
        var p = await db.Platillos.FindAsync(id); if (p == null) return NotFound();
        Cargar(p.IdCategoria); return View("Form", p);
    }

    // Actualiza solo las propiedades editables para no sobrescribir valores de servidor omitidos del formulario.
    // Si no hay archivo nuevo, conserva la foto; quitarImagen la borra solo cuando se marcó explícitamente.
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(MaxImageSize + 64 * 1024)]
    public async Task<IActionResult> Edit(int id, Platillo p, IFormFile? imagen, bool quitarImagen = false)
    {
        if (id != p.IdPlatillo) return BadRequest();
        var actual = await db.Platillos.FindAsync(id);
        if (actual is null) return NotFound();

        // Mantiene la imagen existente si no se carga otra y actualiza solo campos editables del formulario.
        p.Imagen = actual.Imagen;
        p.ImagenContentType = actual.ImagenContentType;
        var uploadedImage = await LeerImagenAsync(imagen);
        if (imagen is not null && uploadedImage is null)
        {
            Cargar(p.IdCategoria);
            return View("Form", p);
        }

        if (!ModelState.IsValid) { Cargar(p.IdCategoria); return View("Form", p); }
        actual.Nombre = p.Nombre;
        actual.Descripcion = p.Descripcion;
        actual.Precio = p.Precio;
        actual.Estado = p.Estado;
        actual.Existencias = p.Existencias;
        actual.IdCategoria = p.IdCategoria;
        if (uploadedImage is not null)
        {
            actual.Imagen = uploadedImage.Value.Contenido;
            actual.ImagenContentType = uploadedImage.Value.Tipo;
        }
        else if (quitarImagen)
        {
            actual.Imagen = null;
            actual.ImagenContentType = null;
        }

        await db.SaveChangesAsync();
        TempData["Ok"] = "Platillo actualizado."; return RedirectToAction(nameof(Index));
    }

    // No borra un platillo referenciado por detalles de órdenes; informa el conflicto de clave foránea.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await db.Platillos.FindAsync(id); if (p == null) return NotFound();
        try { db.Platillos.Remove(p); await db.SaveChangesAsync(); TempData["Ok"] = "Platillo eliminado."; }
        catch (DbUpdateException) { TempData["Error"] = "No se puede eliminar: el platillo tiene órdenes asociadas."; }
        return RedirectToAction(nameof(Index));
    }

    // Lee el archivo a memoria solo después de comprobar longitud; devuelve null si no se adjuntó
    // o agrega un error de validación si la carga infringe tamaño o formato aceptado.
    private async Task<(byte[] Contenido, string Tipo)?> LeerImagenAsync(IFormFile? imagen)
    {
        if (imagen is null || imagen.Length == 0) return null;
        // El límite del archivo se comprueba además del límite de tamaño total de la solicitud.
        if (imagen.Length > MaxImageSize)
        {
            ModelState.AddModelError(nameof(imagen), "La imagen no puede superar los 5 MB.");
            return null;
        }

        await using var stream = new MemoryStream((int)imagen.Length);
        await imagen.CopyToAsync(stream);
        var bytes = stream.ToArray();
        var tipo = DetectarTipo(bytes);
        if (tipo is null)
        {
            ModelState.AddModelError(nameof(imagen), "Usa una imagen válida en formato JPG, PNG o WebP.");
            return null;
        }

        return (bytes, tipo);
    }

    // El MIME se decide por bytes mágicos (no por extensión ni por el Content-Type controlado por el cliente).
    // Reconoce las firmas mínimas de PNG, JPEG y contenedor RIFF/WEBP.
    private static string? DetectarTipo(byte[] bytes)
    {
        // Se verifica la firma binaria y no el nombre o Content-Type enviados por el navegador.
        if (bytes.Length >= 8 &&
            bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        if (bytes.Length >= 12 &&
            bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";

        return null;
    }
}

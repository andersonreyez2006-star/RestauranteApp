using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;
namespace RestauranteApp.Controllers;

// Las acciones de esta clase son públicas para que un usuario no autenticado pueda iniciar sesión.
// La autorización de las demás áreas se evalúa con los claims emitidos aquí y las políticas [Authorize].
[AllowAnonymous]
public class AccountController(AppDbContext db, IPasswordHasher<Usuario> hasher) : Controller
{
    // Muestra el formulario vacío de autenticación.
    public IActionResult Login() => View();

    // Busca la cuenta por correo, valida el hash de contraseña y, si coincide, crea la cookie de sesión.
    // En caso de fallo se presenta un mensaje genérico para no revelar si el correo existe.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string correo, string contrasena)
    {
        // Nunca se compara la contraseña directamente: se valida contra el hash persistido.
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Correo == correo);
        if (u == null || hasher.VerifyHashedPassword(u, u.Contrasena, contrasena ?? "") == PasswordVerificationResult.Failed)
        { ViewBag.Error = "Correo o contraseña incorrectos."; return View(); }
        // Los claims permiten identificar al usuario y aplicar autorización por rol en las demás rutas.
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, u.IdUsuario.ToString()), new(ClaimTypes.Name, u.Nombre), new(ClaimTypes.Role, u.Rol) };
        var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));
        return RedirectToAction("Index", "Platillos");
    }

    // Cierra la sesión invalidando la cookie de autenticación; se invoca mediante POST para evitar logout accidental.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    { await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return RedirectToAction(nameof(Login)); }

    // Destino visible cuando el usuario inició sesión pero no tiene el rol requerido por una ruta.
    public IActionResult Denegado() => View();

    // Mensaje genérico de respaldo para errores de aplicación no controlados por una acción concreta.
    public IActionResult Error() => Content("Ocurrió un error inesperado. Intenta de nuevo.");
}

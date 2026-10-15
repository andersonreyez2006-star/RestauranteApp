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

// Gestiona el inicio y cierre de sesión mediante cookies y contraseñas verificadas con hash.
[AllowAnonymous]
public class AccountController(AppDbContext db, IPasswordHasher<Usuario> hasher) : Controller
{
    public IActionResult Login() => View();

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

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    { await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return RedirectToAction(nameof(Login)); }

    public IActionResult Denegado() => View();
    public IActionResult Error() => Content("Ocurrió un error inesperado. Intenta de nuevo.");
}

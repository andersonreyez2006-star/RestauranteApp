using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;
using RestauranteApp.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHttpClient<AsistenteService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { respuesta = "Hay muchas consultas seguidas. Espera un momento y vuelve a intentarlo." },
            cancellationToken);
    };
    options.AddPolicy("chat", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => { o.LoginPath = "/Account/Login"; o.AccessDeniedPath = "/Account/Denegado"; });
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

// BD con reintentos (SQL Server en Docker tarda en arrancar) + usuarios iniciales con contraseña hasheada
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
    for (var i = 0; i < 15; i++)
    {
        try
        {
            if (db.Database.GetMigrations().Any()) db.Database.Migrate(); else db.Database.EnsureCreated();
            // Actualización aditiva para instalaciones creadas con el prototipo anterior (sin migraciones EF).
            db.Database.ExecuteSqlRaw("IF COL_LENGTH('dbo.Platillos', 'Existencias') IS NULL ALTER TABLE dbo.Platillos ADD Existencias int NOT NULL CONSTRAINT DF_Platillos_Existencias DEFAULT 0; IF COL_LENGTH('dbo.Platillos', 'Imagen') IS NULL ALTER TABLE dbo.Platillos ADD Imagen varbinary(max) NULL; IF COL_LENGTH('dbo.Platillos', 'ImagenContentType') IS NULL ALTER TABLE dbo.Platillos ADD ImagenContentType nvarchar(100) NULL; IF COL_LENGTH('dbo.Ordenes', 'IdUsuario') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Ordenes') AND name = 'IdUsuario' AND is_nullable = 0) ALTER TABLE dbo.Ordenes ALTER COLUMN IdUsuario int NULL;");
            if (!db.Usuarios.Any())
            {
                foreach (var (nombre, rol, clave) in new[] {
                    ("Administrador", "Administrador", "Admin123!"), ("Mesero Demo", "Mesero", "Mesero123!"),
                    ("Cocina Demo", "Cocina", "Cocina123!"), ("Cajero Demo", "Cajero", "Cajero123!") })
                {
                    var u = new Usuario { Nombre = nombre, Rol = rol, Correo = rol.ToLower() + "@restaurante.com" };
                    u.Contrasena = hasher.HashPassword(u, clave);
                    db.Usuarios.Add(u);
                }
                db.SaveChanges();
            }
            break;
        }
        catch when (i < 14) { Thread.Sleep(4000); }
    }
}

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Account/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Platillos}/{action=Index}/{id?}");
app.Run();

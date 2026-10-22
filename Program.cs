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
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(
    builder.Configuration.GetConnectionString("Default"),
    sql => sql.EnableRetryOnFailure(15, TimeSpan.FromSeconds(4), null)));
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
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy("BillingAccess", policy => policy.RequireRole("Administrador", "Cajero"));
});

var app = builder.Build();

// La conexión reintenta errores transitorios; errores de configuración o esquema detienen el arranque.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
    if (db.Database.GetMigrations().Any()) db.Database.Migrate(); else db.Database.EnsureCreated();
    // Actualización aditiva para instalaciones creadas con el prototipo anterior (sin migraciones EF).
    db.Database.ExecuteSqlRaw("""
        IF COL_LENGTH('dbo.Platillos', 'Existencias') IS NULL
            ALTER TABLE dbo.Platillos ADD Existencias int NOT NULL CONSTRAINT DF_Platillos_Existencias DEFAULT 0;
        IF COL_LENGTH('dbo.Platillos', 'Imagen') IS NULL
            ALTER TABLE dbo.Platillos ADD Imagen varbinary(max) NULL;
        IF COL_LENGTH('dbo.Platillos', 'ImagenContentType') IS NULL
            ALTER TABLE dbo.Platillos ADD ImagenContentType nvarchar(100) NULL;
        IF COL_LENGTH('dbo.Ordenes', 'IdUsuario') IS NOT NULL
           AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Ordenes') AND name = 'IdUsuario' AND is_nullable = 0)
            ALTER TABLE dbo.Ordenes ALTER COLUMN IdUsuario int NULL;
        IF COL_LENGTH('dbo.Ordenes', 'TokenPedidoPublico') IS NULL
            ALTER TABLE dbo.Ordenes ADD TokenPedidoPublico nvarchar(64) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Facturas') AND name = 'UX_Facturas_IdOrden')
        BEGIN
            IF EXISTS (SELECT IdOrden FROM dbo.Facturas GROUP BY IdOrden HAVING COUNT(*) > 1)
                THROW 51000, 'Hay órdenes con facturas duplicadas. Corrija esos registros antes de iniciar la aplicación.', 1;
            CREATE UNIQUE INDEX UX_Facturas_IdOrden ON dbo.Facturas(IdOrden);
        END;
        """);
    // SQL Server compiles a batch before applying ALTER TABLE; create the index only after
    // the preceding batch has added TokenPedidoPublico to older installations.
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Ordenes') AND name = 'UX_Ordenes_TokenPedidoPublico')
            CREATE UNIQUE INDEX UX_Ordenes_TokenPedidoPublico ON dbo.Ordenes(TokenPedidoPublico) WHERE TokenPedidoPublico IS NOT NULL;
        """);
    if (!db.Usuarios.Any())
    {
        if (app.Environment.IsDevelopment())
        {
            foreach (var (nombre, rol, clave) in new[] {
                ("Administrador", "Administrador", "Admin123!"), ("Mesero Demo", "Mesero", "Mesero123!"),
                ("Cocina Demo", "Cocina", "Cocina123!"), ("Cajero Demo", "Cajero", "Cajero123!") })
            {
                var demo = new Usuario { Nombre = nombre, Rol = rol, Correo = rol.ToLower() + "@restaurante.com" };
                demo.Contrasena = hasher.HashPassword(demo, clave);
                db.Usuarios.Add(demo);
            }
        }
        else
        {
            var correo = builder.Configuration["InitialAdmin:Email"];
            var contrasena = builder.Configuration["InitialAdmin:Password"];
            if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(contrasena))
                throw new InvalidOperationException(
                    "La base de datos no tiene usuarios. Configure InitialAdmin:Email e InitialAdmin:Password para crear el administrador inicial.");

            var admin = new Usuario { Nombre = "Administrador", Rol = "Administrador", Correo = correo.Trim() };
            admin.Contrasena = hasher.HashPassword(admin, contrasena);
            db.Usuarios.Add(admin);
        }

        db.SaveChanges();
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

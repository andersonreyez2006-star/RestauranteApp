using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;
using RestauranteApp.Data;
using RestauranteApp.Models;
using RestauranteApp.Controllers;
using RestauranteApp.Services;
using Xunit;

namespace RestauranteApp.Tests;

public class AsistenteServiceTests
{
    [Fact]
    public async Task ResponderAsync_ReturnsOnlyPublicMenuInformation()
    {
        await using var db = CreateDatabase();
        var categoria = new Categoria { Nombre = "Postres" };
        db.Categorias.Add(categoria);
        db.Platillos.AddRange(
            new Platillo
            {
                Nombre = "Quesadilla salvadoreña",
                Categoria = categoria,
                Descripcion = "Postre dulce de queso",
                Precio = 2.75m,
                Estado = "Disponible",
                Existencias = 30
            },
            new Platillo
            {
                Nombre = "Pastel agotado",
                Categoria = categoria,
                Descripcion = "No debe aparecer en el chat público",
                Precio = 4.50m,
                Estado = "Agotado",
                Existencias = 0
            });
        await db.SaveChangesAsync();

        var service = new AsistenteService(db);
        var response = await service.ResponderAsync("¿Qué postres hay y cuántos tienen?", CancellationToken.None);

        Assert.Contains("Quesadilla salvadoreña", response);
        Assert.DoesNotContain("Entradas", response);
        Assert.DoesNotContain("Pastel agotado", response);
        Assert.DoesNotContain("No debe aparecer", response);
        Assert.DoesNotContain("30", response);
    }

    [Fact]
    public async Task ResponderAsync_UsesDescriptionForAvailableDishWithoutRevealingStock()
    {
        await using var db = CreateDatabase();
        var categoria = new Categoria { Nombre = "Entradas" };
        db.Categorias.Add(categoria);
        db.Platillos.Add(new Platillo
        {
            Nombre = "Pupusas revueltas",
            Categoria = categoria,
            Descripcion = "Tres pupusas con curtido y salsa",
            Precio = 3.50m,
            Estado = "Disponible",
            Existencias = 50
        });
        await db.SaveChangesAsync();

        var service = new AsistenteService(db);
        var response = await service.ResponderAsync("¿Qué trae Pupusas revueltas?", CancellationToken.None);

        Assert.Contains("Tres pupusas con curtido y salsa", response);
        Assert.True(response.Contains("$3.50", StringComparison.Ordinal) ||
                    response.Contains("$3,50", StringComparison.Ordinal));
        Assert.Contains("disponible", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("50 en existencia", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("50 unidades", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResponderAdministradorAsync_ReturnsSalesAndOpenOrdersWithoutCustomerData()
    {
        await using var db = CreateDatabase();
        var mesa = new Mesa { NumeroMesa = 7, Capacidad = 4, Estado = "Ocupada" };
        var order = new Orden
        {
            Fecha = DateTime.Today,
            Estado = "Pendiente",
            Mesa = mesa,
            Detalles = [new DetalleOrden { Cantidad = 2, PrecioUnitario = 5m, Subtotal = 10m }]
        };
        db.Ordenes.Add(order);
        await db.SaveChangesAsync();
        db.Facturas.Add(new Factura
        {
            Fecha = DateTime.Today.AddHours(12),
            Subtotal = 10m,
            IVA = 1.30m,
            Total = 11.30m,
            IdOrden = order.IdOrden
        });
        await db.SaveChangesAsync();

        var service = new AsistenteService(db);
        var ventas = await service.ResponderAdministradorAsync("¿Cuáles son las ventas de hoy?", CancellationToken.None);
        var ordenes = await service.ResponderAdministradorAsync("¿Qué órdenes están pendientes?", CancellationToken.None);

        Assert.Contains("1 factura", ventas);
        Assert.True(ventas.Contains("$11.30", StringComparison.Ordinal) ||
                    ventas.Contains("$11,30", StringComparison.Ordinal));
        Assert.Contains("mesa 7", ordenes);
        Assert.Contains("Pendiente", ordenes);
        Assert.Contains("2 artículo", ordenes);
        Assert.DoesNotContain("correo", ordenes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResponderAdministradorAsync_ProvidesExactInventoryAndLowStockAlerts()
    {
        await using var db = CreateDatabase();
        var categoria = new Categoria { Nombre = "Entradas" };
        db.Categorias.Add(categoria);
        db.Platillos.AddRange(
            new Platillo
            {
                Nombre = "Pupusas revueltas",
                Categoria = categoria,
                Descripcion = "Tres pupusas con curtido",
                Precio = 3.50m,
                Estado = "Disponible",
                Existencias = 4
            },
            new Platillo
            {
                Nombre = "Pastel agotado",
                Categoria = categoria,
                Precio = 4m,
                Estado = "Agotado",
                Existencias = 0
            });
        await db.SaveChangesAsync();

        var service = new AsistenteService(db);
        var detalle = await service.ResponderAdministradorAsync("Estado de Pupusas revueltas", CancellationToken.None);
        var alertas = await service.ResponderAdministradorAsync("¿Qué platillos tienen inventario bajo?", CancellationToken.None);

        Assert.Contains("existencias 4", detalle);
        Assert.Contains("Disponible", detalle);
        Assert.Contains("Pupusas revueltas (4)", alertas);
        Assert.Contains("Pastel agotado (0)", alertas);
    }

    [Fact]
    public void PreguntarAdministrador_RequiresAdministratorRole()
    {
        var method = typeof(AsistenteController).GetMethod(nameof(AsistenteController.PreguntarAdministrador));
        var publicMethod = typeof(AsistenteController).GetMethod(nameof(AsistenteController.Preguntar));

        var authorization = Assert.IsType<AuthorizeAttribute>(
            method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Single());

        Assert.Equal("Administrador", authorization.Roles);
        Assert.NotNull(publicMethod!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).SingleOrDefault());
        Assert.Empty(typeof(AsistenteController).GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
    }

    private static AppDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}

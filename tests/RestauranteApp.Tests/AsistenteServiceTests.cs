using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;
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

    private static AppDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}

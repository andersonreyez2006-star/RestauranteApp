using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;

namespace RestauranteApp.Services;

public class AsistenteService(AppDbContext db)
{
    private sealed record PlatilloDisponible(string Nombre, string Categoria, string? Descripcion, decimal Precio, int Existencias);

    public async Task<string> ResponderAsync(string pregunta, CancellationToken cancellationToken)
    {
        var platillos = await db.Platillos
            .AsNoTracking()
            .Include(p => p.Categoria)
            .Where(p => p.Estado == "Disponible" && p.Existencias > 0)
            .OrderBy(p => p.Categoria!.Nombre)
            .ThenBy(p => p.Nombre)
            .Select(p => new PlatilloDisponible(
                p.Nombre,
                p.Categoria == null ? "Especialidad" : p.Categoria.Nombre,
                p.Descripcion,
                p.Precio,
                p.Existencias))
            .ToListAsync(cancellationToken);

        return RespuestaFrecuente(pregunta, platillos);
    }

    private static string RespuestaFrecuente(string pregunta, IReadOnlyCollection<PlatilloDisponible> platillos)
    {
        var texto = pregunta.Trim().ToLowerInvariant();
        var textoNormalizado = Normalizar(texto);
        var disponibles = platillos.ToList();

        if (Contiene(textoNormalizado, "gracias", "muchas gracias", "te agradezco"))
        {
            return "¡Con gusto! ¿Quieres consultar algo más del menú o necesitas ayuda para hacer tu pedido?";
        }

        if (Contiene(textoNormalizado, "hola", "buenos dias", "buenas tardes", "buenas noches"))
        {
            return "¡Hola! Puedo ayudarte con el menú, los precios y cómo hacer un pedido en mesa. ¿Qué te gustaría saber?";
        }

        if (Contiene(textoNormalizado, "mas barato", "menor precio", "mas economico", "mas economica"))
        {
            var masBarato = disponibles.OrderBy(p => p.Precio).FirstOrDefault();
            return masBarato is null
                ? "Por el momento no hay platillos disponibles. Consulta nuevamente más tarde."
                : $"El platillo de menor precio es {masBarato.Nombre} y cuesta ${masBarato.Precio:N2}.";
        }

        if (Contiene(textoNormalizado, "mas caro", "mayor precio", "mas costoso", "mas costosa"))
        {
            var masCaro = disponibles.OrderByDescending(p => p.Precio).FirstOrDefault();
            return masCaro is null
                ? "Por el momento no hay platillos disponibles. Consulta nuevamente más tarde."
                : $"El platillo de mayor precio es {masCaro.Nombre} y cuesta ${masCaro.Precio:N2}.";
        }

        if (Contiene(textoNormalizado, "precio", "cuesta", "cuestan", "vale", "valen"))
        {
            var platilloConsultado = disponibles.FirstOrDefault(p =>
                textoNormalizado.Contains(Normalizar(p.Nombre), StringComparison.Ordinal));
            return platilloConsultado is not null
                ? $"{platilloConsultado.Nombre} cuesta ${platilloConsultado.Precio:N2}. ¿Quieres saber algo más sobre el menú?"
                : disponibles.Count == 0
                    ? "Por el momento no hay platillos disponibles. Consulta nuevamente más tarde."
                    : "Estos son los precios actuales: " +
                      string.Join(", ", disponibles.Take(8).Select(p => $"{p.Nombre} (${p.Precio:N2})")) +
                      ". ¿Quieres saber el precio de alguno en particular?";
        }

        if (Contiene(textoNormalizado, "menu", "platillo", "comida", "venden", "ofrecen"))
        {
            return disponibles.Count == 0
                ? "Por el momento no hay platillos disponibles. Consulta nuevamente más tarde."
                : "Estos son algunos platillos disponibles: " +
                  string.Join(", ", disponibles.Take(8).Select(p => $"{p.Nombre} (${p.Precio:N2})")) +
                  ". ¿Te interesa conocer el precio o la disponibilidad de alguno?";
        }

        if (Contiene(textoNormalizado, "pedir", "pedido", "orden", "ordenar", "comprar"))
        {
            return "¡Claro! Abre “Pedir en mesa”, selecciona tu mesa, indica la cantidad de cada platillo y pulsa “Enviar pedido a cocina”. ¿Quieres que te ayude a elegir algo del menú?";
        }

        if (Contiene(textoNormalizado, "mesa", "sentado", "sentada"))
        {
            return "En “Pedir en mesa” puedes seleccionar una mesa libre antes de elegir tus platillos.";
        }

        if (Contiene(textoNormalizado, "ingrediente", "contiene", "alergia", "alergico"))
        {
            return "Las descripciones del menú muestran la información disponible de cada platillo. Para confirmar ingredientes o alérgenos, consulta directamente con el personal.";
        }

        if (Contiene(textoNormalizado, "horario", "abren", "abierto", "cierran", "ubicacion", "direccion", "pago", "pagar"))
        {
            return "No tengo esa información confirmada. Por favor, consulta directamente con el personal del restaurante.";
        }

        return "Puedo ayudarte con los platillos, precios y cómo hacer un pedido en mesa. Cuéntame qué necesitas saber y te ayudo.";
    }

    private static string Normalizar(string texto) =>
        string.Concat(texto.Normalize(NormalizationForm.FormD)
            .Where(caracter => CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark))
            .Normalize(NormalizationForm.FormC);

    private static bool Contiene(string texto, params string[] palabras) =>
        palabras.Any(texto.Contains);
}

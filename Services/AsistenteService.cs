using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;

namespace RestauranteApp.Services;

public class AsistenteService(AppDbContext db)
{
    private sealed record PlatilloMenu(
        string Nombre,
        string Categoria,
        string? Descripcion,
        decimal Precio,
        string Estado,
        int Existencias);

    public async Task<string> ResponderAsync(string pregunta, CancellationToken cancellationToken)
    {
        var platillos = await db.Platillos
            .AsNoTracking()
            .Include(p => p.Categoria)
            .OrderBy(p => p.Categoria!.Nombre)
            .ThenBy(p => p.Nombre)
            .Select(p => new PlatilloMenu(
                p.Nombre,
                p.Categoria == null ? "Especialidad" : p.Categoria.Nombre,
                p.Descripcion,
                p.Precio,
                p.Estado,
                p.Existencias))
            .ToListAsync(cancellationToken);

        return RespuestaFrecuente(pregunta, platillos);
    }

    private static string RespuestaFrecuente(string pregunta, IReadOnlyCollection<PlatilloMenu> platillos)
    {
        var textoNormalizado = Normalizar(pregunta.Trim().ToLowerInvariant());
        var menu = platillos.ToList();
        var disponibles = menu
            .Where(p => p.Estado.Equals("Disponible", StringComparison.OrdinalIgnoreCase) && p.Existencias > 0)
            .ToList();

        if (Contiene(textoNormalizado, "gracias", "muchas gracias", "te agradezco"))
            return "¡Con gusto! ¿Quieres consultar algo más del menú o necesitas ayuda para hacer tu pedido?";

        if (Contiene(textoNormalizado, "hola", "buenos dias", "buenas tardes", "buenas noches"))
            return "¡Hola! Puedo mostrarte platillos disponibles, precios, categorías y descripciones, o explicarte cómo pedir en mesa. ¿Qué se te antoja?";

        var platilloConsultado = menu.FirstOrDefault(p =>
            textoNormalizado.Contains(Normalizar(p.Nombre), StringComparison.Ordinal));

        if (platilloConsultado is not null &&
            Contiene(textoNormalizado, "descripcion", "que trae", "que contiene", "como es", "informacion", "detalle"))
        {
            var descripcion = string.IsNullOrWhiteSpace(platilloConsultado.Descripcion)
                ? "No hay una descripción registrada para este platillo."
                : platilloConsultado.Descripcion;
            return $"{platilloConsultado.Nombre}: {descripcion} Precio: ${platilloConsultado.Precio:N2}. " +
                   FormatoDisponibilidad(platilloConsultado);
        }

        if (Contiene(textoNormalizado, "categoria", "categorias", "tipo de comida", "tipos de comida"))
        {
            var categoriaConsultada = menu
                .Select(p => p.Categoria)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(categoria =>
                    textoNormalizado.Contains(Normalizar(categoria), StringComparison.Ordinal));

            if (categoriaConsultada is not null)
            {
                var platillosCategoria = disponibles
                    .Where(p => p.Categoria.Equals(categoriaConsultada, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                return platillosCategoria.Count == 0
                    ? $"Por el momento no hay platillos disponibles en la categoría {categoriaConsultada}."
                    : $"En {categoriaConsultada} tenemos: {string.Join(", ", platillosCategoria.Select(FormatoPlatillo))}. ¿Quieres conocer la descripción de alguno?";
            }

            var categorias = disponibles.Select(p => p.Categoria).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return categorias.Count == 0
                ? "Por el momento no hay categorías con platillos disponibles."
                : $"Tenemos estas categorías: {string.Join(", ", categorias)}. ¿Cuál te gustaría explorar?";
        }

        var categoriaMencionada = disponibles
            .Select(p => p.Categoria)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(categoria =>
                textoNormalizado.Contains(Normalizar(categoria), StringComparison.Ordinal));
        if (categoriaMencionada is not null)
        {
            var platillosCategoria = disponibles
                .Where(p => p.Categoria.Equals(categoriaMencionada, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return $"En {categoriaMencionada} tenemos: {string.Join(", ", platillosCategoria.Select(FormatoPlatillo))}. ¿Quieres conocer la descripción de alguno?";
        }

        if (Contiene(textoNormalizado, "mas barato", "menor precio", "mas economico", "mas economica"))
        {
            var masBarato = disponibles.OrderBy(p => p.Precio).FirstOrDefault();
            return masBarato is null
                ? "Por el momento no hay platillos disponibles. Consulta nuevamente más tarde."
                : $"La opción de menor precio es {FormatoPlatillo(masBarato)}. ¿Quieres ver otras opciones económicas?";
        }

        if (Contiene(textoNormalizado, "mas caro", "mayor precio", "mas costoso", "mas costosa"))
        {
            var masCaro = disponibles.OrderByDescending(p => p.Precio).FirstOrDefault();
            return masCaro is null
                ? "Por el momento no hay platillos disponibles. Consulta nuevamente más tarde."
                : $"La opción de mayor precio es {FormatoPlatillo(masCaro)}. ¿Quieres conocer su descripción?";
        }

        if (Contiene(textoNormalizado, "precio", "cuesta", "cuestan", "vale", "valen"))
        {
            return platilloConsultado is not null
                ? $"{FormatoPlatillo(platilloConsultado)}. {FormatoDisponibilidad(platilloConsultado)} ¿Quieres saber algo más sobre el menú?"
                : disponibles.Count == 0
                    ? "Por el momento no hay platillos disponibles. Consulta nuevamente más tarde."
                    : "Estos son los precios actuales: " +
                      string.Join(", ", disponibles.Take(8).Select(FormatoPlatillo)) +
                      ". ¿Quieres saber el precio de alguno en particular?";
        }

        if (Contiene(textoNormalizado, "mesa", "sentado", "sentada"))
            return "En “Pedir en mesa” puedes elegir una mesa libre y después agregar los platillos que quieras. Si no aparece una mesa disponible, consulta con el personal.";

        if (Contiene(textoNormalizado, "disponible", "disponibilidad", "hay", "existencia", "agotado", "agotada", "tienen"))
        {
            if (platilloConsultado is not null)
                return $"{platilloConsultado.Nombre}: {FormatoDisponibilidad(platilloConsultado)}";

            return disponibles.Count == 0
                ? "Por el momento no hay platillos disponibles."
                : "Sí, tenemos estas opciones disponibles: " +
                  string.Join(", ", disponibles.Take(8).Select(FormatoPlatillo)) +
                  ". Dime el nombre de un platillo si quieres confirmar uno en particular.";
        }

        if (Contiene(textoNormalizado, "recomienda", "recomendacion", "sugerencia", "sugiere", "que me recomiendas", "que puedo comer"))
        {
            var sugerencias = disponibles.OrderBy(p => p.Precio).Take(3).ToList();
            return sugerencias.Count == 0
                ? "Por el momento no hay platillos disponibles para recomendarte."
                : "Como sugerencias, puedes probar: " + string.Join(", ", sugerencias.Select(FormatoPlatillo)) +
                  ". Son opciones del menú actual. ¿Prefieres que te muestre entradas, platos fuertes, postres o bebidas?";
        }

        if (Contiene(textoNormalizado, "menu", "platillo", "comida", "venden", "ofrecen"))
        {
            return disponibles.Count == 0
                ? "Por el momento no hay platillos disponibles. Consulta nuevamente más tarde."
                : "Estos son algunos platillos disponibles: " +
                  string.Join(", ", disponibles.Take(8).Select(FormatoPlatillo)) +
                  ". También puedo contarte sus descripciones, mostrarte categorías o recomendarte opciones.";
        }

        if (Contiene(textoNormalizado, "pedir", "pedido", "orden", "ordenar", "comprar", "como funciona"))
            return "¡Claro! Abre “Pedir en mesa”, elige una mesa libre, agrega las cantidades de los platillos y pulsa “Enviar pedido a cocina”. Antes, si quieres, te ayudo a revisar el menú.";

        if (Contiene(textoNormalizado, "ingrediente", "contiene", "alergia", "alergico"))
            return "Las descripciones muestran la información registrada del platillo. Para confirmar ingredientes, contaminación cruzada o alérgenos, consulta al personal antes de ordenar.";

        if (Contiene(textoNormalizado, "horario", "abren", "abierto", "cierran", "ubicacion", "direccion", "pago", "pagar"))
            return "No tengo esa información confirmada. Por favor, consulta directamente con el personal del restaurante.";

        return "Puedo ayudarte a consultar platillos, precios, disponibilidad, descripciones y categorías, darte algunas sugerencias o explicarte cómo hacer un pedido. ¿Qué te gustaría saber?";
    }

    private static string FormatoPlatillo(PlatilloMenu platillo) =>
        $"{platillo.Nombre} (${platillo.Precio:N2})";

    private static string FormatoDisponibilidad(PlatilloMenu platillo) =>
        platillo.Estado.Equals("Disponible", StringComparison.OrdinalIgnoreCase) && platillo.Existencias > 0
            ? $"está disponible ({platillo.Existencias} en existencia)."
            : "no está disponible por el momento.";

    private static string Normalizar(string texto) =>
        string.Concat(texto.Normalize(NormalizationForm.FormD)
            .Where(caracter => CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark))
            .Normalize(NormalizationForm.FormC);

    private static bool Contiene(string texto, params string[] palabras) =>
        palabras.Any(texto.Contains);
}

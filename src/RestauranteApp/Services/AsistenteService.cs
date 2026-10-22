using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;

namespace RestauranteApp.Services;

public class AsistenteService(AppDbContext db, IClimaService climaService)
{
    private sealed record PlatilloPublico(string Nombre, string Categoria, string? Descripcion, decimal Precio);
    private sealed record PlatilloAdministrador(
        string Nombre,
        string Categoria,
        string? Descripcion,
        decimal Precio,
        string Estado,
        int Existencias);

    public async Task<string> ResponderAdministradorAsync(string pregunta, CancellationToken cancellationToken)
    {
        var textoNormalizado = Normalizar(pregunta.Trim());

        if (Contiene(textoNormalizado, "hola", "buenos dias", "buenas tardes", "buenas noches"))
            return "Hola. Puedo consultar ventas de hoy, órdenes abiertas, inventario, existencias y datos de platillos. Las consultas son de solo lectura.";

        if (ContienePalabra(textoNormalizado, "venta", "ventas", "factura", "facturado", "ingreso", "ingresos"))
        {
            var desde = DateTime.Today;
            var hasta = desde.AddDays(1);
            var facturas = db.Facturas.AsNoTracking().Where(f => f.Fecha >= desde && f.Fecha < hasta);
            var cantidad = await facturas.CountAsync(cancellationToken);
            var total = await facturas.SumAsync(f => (decimal?)f.Total, cancellationToken) ?? 0;

            if (cantidad == 0)
                return $"Hoy ({desde:dd/MM/yyyy}) todavía no hay ventas facturadas.";

            return $"Hoy ({desde:dd/MM/yyyy}) se emitieron {cantidad} factura(s), por un total de ${total:N2}.";
        }

        if (ContienePalabra(textoNormalizado, "orden", "ordenes", "pedido", "pedidos", "pendiente", "pendientes", "preparacion", "cocina"))
        {
            var ordenes = await db.Ordenes.AsNoTracking()
                .Include(o => o.Mesa)
                .Include(o => o.Detalles)
                .Where(o => o.Estado != "Facturada")
                .OrderBy(o => o.Fecha)
                .Take(5)
                .ToListAsync(cancellationToken);
            var cantidadAbiertas = await db.Ordenes.AsNoTracking()
                .CountAsync(o => o.Estado != "Facturada", cancellationToken);

            if (cantidadAbiertas == 0)
                return "No hay órdenes abiertas en este momento.";

            var resumen = string.Join("; ", ordenes.Select(o =>
                $"orden #{o.IdOrden}, mesa {o.Mesa?.NumeroMesa.ToString() ?? "sin mesa"}, {o.Estado}, {o.Detalles.Sum(d => d.Cantidad)} artículo(s)"));
            var restantes = cantidadAbiertas > ordenes.Count
                ? $" Muestro las primeras {ordenes.Count} de {cantidadAbiertas} órdenes abiertas."
                : "";
            return $"Órdenes abiertas: {resumen}.{restantes}";
        }

        var platillos = await db.Platillos.AsNoTracking()
            .OrderBy(p => p.Existencias)
            .ThenBy(p => p.Nombre)
            .Select(p => new PlatilloAdministrador(
                p.Nombre,
                p.Categoria == null ? "Especialidad" : p.Categoria.Nombre,
                p.Descripcion,
                p.Precio,
                p.Estado,
                p.Existencias))
            .ToListAsync(cancellationToken);
        var platilloConsultado = platillos.FirstOrDefault(p =>
            textoNormalizado.Contains(Normalizar(p.Nombre), StringComparison.Ordinal));

        if (platilloConsultado is not null)
        {
            var descripcion = string.IsNullOrWhiteSpace(platilloConsultado.Descripcion)
                ? "Sin descripción registrada"
                : platilloConsultado.Descripcion;
            return $"{platilloConsultado.Nombre} ({platilloConsultado.Categoria}): {descripcion}. " +
                   $"Precio ${platilloConsultado.Precio:N2}; estado {platilloConsultado.Estado}; existencias {platilloConsultado.Existencias}.";
        }

        if (Contiene(textoNormalizado, "bajo", "bajos", "poco", "pocos", "agotado", "agotados", "alerta", "reponer", "reabastecer"))
        {
            var bajoStock = platillos.Where(p => p.Existencias <= 5).ToList();
            if (bajoStock.Count == 0)
                return "No hay platillos con cinco existencias o menos.";

            return "Platillos con cinco existencias o menos: " +
                   string.Join(", ", bajoStock.Select(p => $"{p.Nombre} ({p.Existencias})")) + ".";
        }

        if (Contiene(textoNormalizado, "inventario", "existencia", "existencias", "stock", "platillo", "platillos", "menu"))
        {
            var inventario = await db.Platillos.AsNoTracking()
                .OrderBy(p => p.Nombre)
                .Select(p => new { p.Nombre, p.Estado, p.Existencias, p.Precio })
                .ToListAsync(cancellationToken);
            return inventario.Count == 0
                ? "No hay platillos registrados en el inventario."
                : "Inventario actual: " + string.Join(", ", inventario.Select(p =>
                    $"{p.Nombre}: {p.Existencias} existencia(s), {p.Estado}, ${p.Precio:N2}")) + ".";
        }

        return "Puedo consultar ventas de hoy, órdenes abiertas, inventario, existencias bajas o el estado de un platillo. No puedo modificar registros.";
    }

    public async Task<string> ResponderAsync(string pregunta, CancellationToken cancellationToken)
    {
        var preguntaNormalizada = Normalizar(pregunta.Trim());
        if (ContienePalabra(preguntaNormalizada, "clima", "temperatura", "lluvia", "llueve", "lloviendo", "pronostico"))
        {
            var ciudad = ExtraerCiudadClima(pregunta);
            return ciudad is null
                ? "¿De qué ciudad quieres consultar el clima? Por ejemplo: “clima en San Salvador”."
                : await climaService.ConsultarAsync(ciudad, cancellationToken);
        }

        var platillos = await db.Platillos
            .AsNoTracking()
            .Include(p => p.Categoria)
            .Where(p => p.Estado == "Disponible" && p.Existencias > 0 && p.Precio > 0)
            .OrderBy(p => p.Categoria!.Nombre)
            .ThenBy(p => p.Nombre)
            .Select(p => new PlatilloPublico(
                p.Nombre,
                p.Categoria == null ? "Especialidad" : p.Categoria.Nombre,
                p.Descripcion,
                p.Precio))
            .ToListAsync(cancellationToken);

        return RespuestaFrecuente(pregunta, platillos);
    }

    private static string? ExtraerCiudadClima(string pregunta)
    {
        var coincidencia = Regex.Match(
            pregunta,
            @"\ben\s+(.+?)(?:\s+(?:hoy|ahora|por favor))?[?.!]*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return coincidencia.Success
            ? coincidencia.Groups[1].Value.Trim().Trim('¿', '?', '¡', '!', '.', ',', ';', ':')
            : null;
    }

    private static string RespuestaFrecuente(string pregunta, IReadOnlyCollection<PlatilloPublico> platillos)
    {
        var textoNormalizado = Normalizar(pregunta.Trim().ToLowerInvariant());
        var disponibles = platillos.ToList();

        if (Contiene(textoNormalizado, "gracias", "muchas gracias", "te agradezco"))
            return "¡Con gusto! ¿Quieres consultar algo más del menú o necesitas ayuda para hacer tu pedido?";

        if (Contiene(textoNormalizado, "hola", "buenos dias", "buenas tardes", "buenas noches"))
            return "¡Hola! Puedo mostrarte platillos disponibles, precios, categorías y descripciones, o explicarte cómo pedir en mesa. ¿Qué se te antoja?";

        var platilloConsultado = disponibles.FirstOrDefault(p =>
            textoNormalizado.Contains(Normalizar(p.Nombre), StringComparison.Ordinal));

        if (platilloConsultado is not null &&
            Contiene(textoNormalizado, "descripcion", "que trae", "que contiene", "como es", "informacion", "detalle"))
        {
            var descripcion = string.IsNullOrWhiteSpace(platilloConsultado.Descripcion)
                ? "No hay una descripción registrada para este platillo."
                : platilloConsultado.Descripcion;
            return $"{platilloConsultado.Nombre}: {descripcion} Precio: ${platilloConsultado.Precio:N2}. Actualmente está disponible.";
        }

        if (Contiene(textoNormalizado, "categoria", "categorias", "tipo de comida", "tipos de comida"))
        {
            var categoriaConsultada = disponibles
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
                ? $"{FormatoPlatillo(platilloConsultado)}. Actualmente está disponible. ¿Quieres saber algo más sobre el menú?"
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
                return $"{platilloConsultado.Nombre} está disponible actualmente. ¿Quieres consultar su descripción o precio?";

            return disponibles.Count == 0
                ? "Por el momento no hay platillos disponibles."
                : "Sí, tenemos estas opciones disponibles: " +
                  string.Join(", ", disponibles.Take(8).Select(FormatoPlatillo)) +
                  ". Solo muestro las opciones disponibles ahora. Dime el nombre de un platillo para consultar su información.";
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

    private static string FormatoPlatillo(PlatilloPublico platillo) =>
        $"{platillo.Nombre} (${platillo.Precio:N2})";

    private static string Normalizar(string texto) =>
        string.Concat(texto.ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(caracter => CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark))
            .Normalize(NormalizationForm.FormC);

    private static bool Contiene(string texto, params string[] palabras) =>
        palabras.Any(texto.Contains);

    private static bool ContienePalabra(string texto, params string[] palabras)
    {
        foreach (var palabra in palabras)
        {
            var inicio = 0;
            while ((inicio = texto.IndexOf(palabra, inicio, StringComparison.Ordinal)) >= 0)
            {
                var fin = inicio + palabra.Length;
                var inicioDePalabra = inicio == 0 || !char.IsLetterOrDigit(texto[inicio - 1]);
                var finDePalabra = fin == texto.Length || !char.IsLetterOrDigit(texto[fin]);
                if (inicioDePalabra && finDePalabra)
                    return true;
                inicio = fin;
            }
        }

        return false;
    }
}

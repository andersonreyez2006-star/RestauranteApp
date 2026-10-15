using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;

namespace RestauranteApp.Services;

public class AsistenteService(
    AppDbContext db,
    HttpClient http,
    IConfiguration configuration,
    ILogger<AsistenteService> logger)
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

        // Endpoint debe ser la base OpenAI-compatible de Foundry, por ejemplo:
        // https://dspbot.services.ai.azure.com/openai/v1
        var endpoint = configuration["AI:Endpoint"];
        var key = configuration["AI:ApiKey"];
        var model = configuration["AI:Deployment"];
        if (string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(key) ||
            string.IsNullOrWhiteSpace(model))
        {
            return RespuestaFrecuente(pregunta, platillos);
        }

        var menu = string.Join("\n", platillos.Select(p =>
            $"- {p.Nombre}, categoría {p.Categoria}, ${p.Precio:N2}, {p.Existencias} disponibles. {p.Descripcion}"));
        var instructions = """
                            Eres el asistente amable de un restaurante salvadoreño. Responde siempre en español, de forma breve y útil.
                            Puedes resolver dudas sobre cómo consultar el menú, seleccionar una mesa y enviar un pedido: el cliente elige una mesa, indica cantidades y pulsa "Enviar pedido a cocina".
                            Para precios, ingredientes, categorías o disponibilidad, usa exclusivamente el menú proporcionado. No inventes horarios, dirección, métodos de pago, tiempos de espera, políticas, ingredientes ni información de pedidos.
                            Si no hay datos suficientes, dilo claramente y recomienda consultar al personal. Nunca solicites contraseñas ni datos de pago.
                            Menú disponible:
                            """ + menu;
        var payload = new
        {
            model,
            input = new object[]
            {
                new
                {
                    role = "system",
                    content = instructions
                },
                new { role = "user", content = pregunta }
            },
            max_output_tokens = 250
        };

        var url = $"{endpoint.TrimEnd('/')}/responses";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("El servicio de IA respondió con el estado {StatusCode}; se usará la respuesta frecuente.", response.StatusCode);
                return RespuestaFrecuente(pregunta, platillos);
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var answer = ObtenerTextoRespuesta(document.RootElement);

            return string.IsNullOrWhiteSpace(answer)
                ? RespuestaFrecuente(pregunta, platillos)
                : answer.Trim();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "No se pudo contactar el servicio de IA; se usará la respuesta frecuente.");
            return RespuestaFrecuente(pregunta, platillos);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "La consulta al servicio de IA agotó el tiempo; se usará la respuesta frecuente.");
            return RespuestaFrecuente(pregunta, platillos);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "El servicio de IA devolvió una respuesta no válida; se usará la respuesta frecuente.");
            return RespuestaFrecuente(pregunta, platillos);
        }
    }

    private static string? ObtenerTextoRespuesta(JsonElement response)
    {
        if (!response.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return null;

        var text = new StringBuilder();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var itemType) ||
                itemType.GetString() != "message" ||
                !item.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var partType) &&
                    partType.GetString() == "output_text" &&
                    part.TryGetProperty("text", out var value) &&
                    value.ValueKind == JsonValueKind.String)
                {
                    text.Append(value.GetString());
                }
            }
        }

        return text.Length == 0 ? null : text.ToString();
    }

    private static string RespuestaFrecuente(string pregunta, IReadOnlyCollection<PlatilloDisponible> platillos)
    {
        var texto = pregunta.Trim().ToLowerInvariant();

        if (Contiene(texto, "menu", "menú", "platillo", "platillos", "comida", "venden", "ofrecen"))
        {
            var disponibles = platillos.ToList();
            return disponibles.Count == 0
                ? "Por el momento no hay platillos disponibles. Consulta nuevamente más tarde."
                : "Puedes ver el menú actualizado en la sección Menú. Por ahora tenemos: " +
                  string.Join(", ", disponibles.Take(6).Select(p => $"{p.Nombre} (${p.Precio:N2})")) +
                  ". Los precios y existencias se muestran junto a cada platillo.";
        }

        if (Contiene(texto, "pedir", "pedido", "orden", "ordenar", "comprar"))
        {
            return "Para pedir, abre “Pedir en mesa”, selecciona tu mesa, indica la cantidad de cada platillo y pulsa “Enviar pedido a cocina”.";
        }

        if (Contiene(texto, "mesa", "sentado", "sentada"))
        {
            return "En “Pedir en mesa” puedes seleccionar una mesa libre antes de elegir tus platillos.";
        }

        if (Contiene(texto, "precio", "cuesta", "cuestan", "vale", "valen"))
        {
            return "Los precios vigentes aparecen en las tarjetas del menú. Dime el nombre del platillo o revisa la sección Menú.";
        }

        if (Contiene(texto, "ingrediente", "contiene", "alergia", "alérgico", "alergico"))
        {
            return "Las descripciones del menú muestran la información disponible de cada platillo. Para confirmar ingredientes o alérgenos, consulta directamente con el personal.";
        }

        if (Contiene(texto, "horario", "abren", "abierto", "cierran", "ubicacion", "ubicación", "direccion", "dirección", "pago", "pagar"))
        {
            return "No tengo esa información confirmada. Por favor, consulta directamente con el personal del restaurante.";
        }

        return "Puedo ayudarte con el menú, precios y cómo hacer un pedido en mesa. Para otra consulta específica, pregunta al personal del restaurante.";
    }

    private static bool Contiene(string texto, params string[] palabras) =>
        palabras.Any(texto.Contains);
}

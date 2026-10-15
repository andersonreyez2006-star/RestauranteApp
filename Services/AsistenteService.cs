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
            logger.LogWarning("Falta configuración de Foundry; se usará la respuesta frecuente.");
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
                var providerError = await LeerCodigoErrorAsync(response, cancellationToken);
                logger.LogWarning(
                    "Foundry respondió con HTTP {StatusCode} y código {ProviderError}; se usará la respuesta frecuente.",
                    (int)response.StatusCode,
                    providerError ?? "no disponible");
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

    private static async Task<string?> LeerCodigoErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in new[] { "code", "type" })
                {
                    if (error.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
                        return value.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // El cuerpo no JSON no cambia el manejo del error HTTP; no se registra para evitar exponer datos.
        }

        return null;
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
            .Where(caracter => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(caracter) !=
                               System.Globalization.UnicodeCategory.NonSpacingMark))
        .Normalize(NormalizationForm.FormC);

    private static bool Contiene(string texto, params string[] palabras) =>
        palabras.Any(texto.Contains);
}

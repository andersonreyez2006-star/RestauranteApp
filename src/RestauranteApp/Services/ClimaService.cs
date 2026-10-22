using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RestauranteApp.Services;

public interface IClimaService
{
    Task<string> ConsultarAsync(string ciudad, CancellationToken cancellationToken);
}

public sealed class ClimaService(HttpClient httpClient, ILogger<ClimaService> logger) : IClimaService
{
    public async Task<string> ConsultarAsync(string ciudad, CancellationToken cancellationToken)
    {
        try
        {
            var ubicacion = await httpClient.GetFromJsonAsync<RespuestaGeocodificacion>(
                $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(ciudad)}&count=1&language=es&format=json",
                cancellationToken);
            var resultadoUbicacion = ubicacion?.Resultados?.FirstOrDefault();
            if (resultadoUbicacion is null)
                return $"No encontré “{ciudad}”. Prueba con el nombre de una ciudad o municipio.";

            var latitud = resultadoUbicacion.Latitud.ToString(CultureInfo.InvariantCulture);
            var longitud = resultadoUbicacion.Longitud.ToString(CultureInfo.InvariantCulture);
            var clima = await httpClient.GetFromJsonAsync<RespuestaPronostico>(
                $"https://api.open-meteo.com/v1/forecast?latitude={latitud}&longitude={longitud}&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m&timezone=auto",
                cancellationToken);

            if (clima?.Actual is null)
            {
                logger.LogWarning("Open-Meteo devolvió una respuesta sin condiciones actuales para {City}", ciudad);
                return "El proveedor de clima no devolvió datos actuales. Intenta nuevamente más tarde.";
            }

            var nombreLugar = string.Join(", ", new[]
            {
                resultadoUbicacion.Nombre,
                resultadoUbicacion.Region,
                resultadoUbicacion.Pais
            }.Where(parte => !string.IsNullOrWhiteSpace(parte)).Distinct(StringComparer.OrdinalIgnoreCase));

            return $"Clima actual en {nombreLugar}: {DescribirCodigo(clima.Actual.CodigoClima)}, " +
                   $"{clima.Actual.Temperatura.ToString("N1", CultureInfo.CurrentCulture)} °C " +
                   $"(sensación {clima.Actual.Sensacion.ToString("N1", CultureInfo.CurrentCulture)} °C), " +
                   $"humedad {clima.Actual.Humedad} %, precipitación {clima.Actual.Precipitacion.ToString("N1", CultureInfo.CurrentCulture)} mm " +
                   $"y viento {clima.Actual.Viento.ToString("N1", CultureInfo.CurrentCulture)} km/h. Fuente: Open-Meteo.";
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "No se pudo consultar Open-Meteo para {City}", ciudad);
            return "No pude conectar con el servicio de clima ahora. Intenta nuevamente más tarde.";
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Open-Meteo devolvió una respuesta no válida para {City}", ciudad);
            return "El servicio de clima devolvió una respuesta no válida. Intenta nuevamente más tarde.";
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "La consulta de clima agotó su tiempo para {City}", ciudad);
            return "La consulta de clima tardó demasiado. Intenta nuevamente más tarde.";
        }
    }

    private static string DescribirCodigo(int codigo) => codigo switch
    {
        0 => "despejado",
        1 => "mayormente despejado",
        2 => "parcialmente nublado",
        3 => "nublado",
        45 or 48 => "niebla",
        51 or 53 or 55 => "llovizna",
        56 or 57 => "llovizna engelante",
        61 or 63 or 65 => "lluvia",
        66 or 67 => "lluvia engelante",
        71 or 73 or 75 or 77 => "nieve",
        80 or 81 or 82 => "chubascos",
        85 or 86 => "chubascos de nieve",
        95 or 96 or 99 => "tormenta",
        _ => "condiciones variables"
    };

    private sealed record RespuestaGeocodificacion(
        [property: JsonPropertyName("results")] List<ResultadoUbicacion>? Resultados);

    private sealed record ResultadoUbicacion(
        [property: JsonPropertyName("name")] string Nombre,
        [property: JsonPropertyName("admin1")] string? Region,
        [property: JsonPropertyName("country")] string? Pais,
        [property: JsonPropertyName("latitude")] double Latitud,
        [property: JsonPropertyName("longitude")] double Longitud);

    private sealed record RespuestaPronostico(
        [property: JsonPropertyName("current")] ClimaActual? Actual);

    private sealed record ClimaActual(
        [property: JsonPropertyName("temperature_2m")] decimal Temperatura,
        [property: JsonPropertyName("relative_humidity_2m")] int Humedad,
        [property: JsonPropertyName("apparent_temperature")] decimal Sensacion,
        [property: JsonPropertyName("precipitation")] decimal Precipitacion,
        [property: JsonPropertyName("weather_code")] int CodigoClima,
        [property: JsonPropertyName("wind_speed_10m")] decimal Viento);
}

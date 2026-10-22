using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using RestauranteApp.Services;
using Xunit;

namespace RestauranteApp.Tests;

public class ClimaServiceTests
{
    [Fact]
    public async Task ConsultarAsync_UsesOpenMeteoWithoutApiKeyAndFormatsCurrentWeather()
    {
        var requests = new List<Uri>();
        var handler = new RespuestaHttpHandler((request, _) =>
        {
            requests.Add(request.RequestUri!);
            var json = request.RequestUri!.Host == "geocoding-api.open-meteo.com"
                ? """{"results":[{"name":"San Salvador","admin1":"San Salvador","country":"El Salvador","latitude":13.69,"longitude":-89.19}]}"""
                : """{"current":{"temperature_2m":28.4,"relative_humidity_2m":65,"apparent_temperature":30.1,"precipitation":0.0,"weather_code":1,"wind_speed_10m":12.5}}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        });
        using var client = new HttpClient(handler);
        var service = new ClimaService(client, NullLogger<ClimaService>.Instance);

        var response = await service.ConsultarAsync("San Salvador", CancellationToken.None);

        Assert.Contains("San Salvador, El Salvador", response);
        Assert.Contains("mayormente despejado", response);
        Assert.True(response.Contains("28.4", StringComparison.Ordinal) ||
                    response.Contains("28,4", StringComparison.Ordinal));
        Assert.Contains("Open-Meteo", response);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.DoesNotContain("key=", request.Query, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ConsultarAsync_ReturnsClearMessageWhenCityIsNotFound()
    {
        var handler = new RespuestaHttpHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"results":[]}""")
            }));
        using var client = new HttpClient(handler);
        var service = new ClimaService(client, NullLogger<ClimaService>.Instance);

        var response = await service.ConsultarAsync("Ciudad inexistente", CancellationToken.None);

        Assert.Contains("No encontré", response);
        Assert.Contains("Ciudad inexistente", response);
    }

    private sealed class RespuestaHttpHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request, cancellationToken);
    }
}

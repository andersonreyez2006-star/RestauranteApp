using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RestauranteApp.Data;
using RestauranteApp.Models;
namespace RestauranteApp.Services;

/// Asistente de IA: usa Azure OpenAI si hay credenciales; si no (o si falla), usa un respaldo local por palabras clave.
public class RecomendacionService(AppDbContext db, HttpClient http, IConfiguration cfg, ILogger<RecomendacionService> log)
{
    public async Task<string> RecomendarAsync(string pregunta)
    {
        var menu = await db.Platillos.Include(p => p.Categoria).Where(p => p.Estado == "Disponible" && p.Existencias > 0).ToListAsync();
        var endpoint = cfg["AI:Endpoint"]; var key = cfg["AI:ApiKey"]; var dep = cfg["AI:Deployment"];
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(dep))
        {
            try
            {
                var lista = string.Join("\n", menu.Select(p => $"- {p.Nombre} ({p.Categoria?.Nombre}) ${p.Precio}; {p.Existencias} disponibles: {p.Descripcion}"));
                var body = new
                {
                    messages = new object[] {
                        new { role = "system", content = "Eres el agente del menú de un restaurante. Recomienda SOLO platillos de esta lista, indica precios y existencias tal como aparecen y no inventes información. Responde en español y en máximo 4 oraciones:\n" + lista },
                        new { role = "user", content = pregunta } },
                    max_tokens = 300, temperature = 0.7
                };
                var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{dep}/chat/completions?api-version=2024-02-01";
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
                req.Headers.Add("api-key", key);
                using var res = await http.SendAsync(req);
                res.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            }
            catch (Exception ex) { log.LogWarning(ex, "IA externa no disponible, usando respaldo local"); }
        }
        return Respaldo(pregunta, menu);
    }

    private static string Respaldo(string pregunta, List<Platillo> menu)
    {
        var palabras = pregunta.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 3).ToList();
        var top = menu.Select(p => new { p, score = palabras.Count(w => $"{p.Nombre} {p.Descripcion} {p.Categoria?.Nombre}".ToLowerInvariant().Contains(w)) })
                      .OrderByDescending(x => x.score).ThenBy(x => x.p.Precio).Take(3).Select(x => x.p).ToList();
        return top.Count == 0 ? "Aún no hay platillos disponibles."
            : "Te sugiero: " + string.Join("; ", top.Select(p => $"{p.Nombre} (${p.Precio}, {p.Existencias} disponibles)")) + ".";
    }
}


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestauranteApp.Services;
namespace RestauranteApp.Controllers;

public record PreguntaDto(string Texto);

[AllowAnonymous]
public class AsistenteController(RecomendacionService svc) : Controller
{
    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> Preguntar([FromBody] PreguntaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto?.Texto)) return BadRequest(new { respuesta = "Escribe tu consulta." });
        return Json(new { respuesta = await svc.RecomendarAsync(dto.Texto) });
    }
}

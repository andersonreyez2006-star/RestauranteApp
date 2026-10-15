using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestauranteApp.Services;

namespace RestauranteApp.Controllers;

public record PreguntaDto([Required, StringLength(500)] string Texto);

[AllowAnonymous]
[EnableRateLimiting("chat")]
[Route("[controller]/[action]")]
public class AsistenteController(AsistenteService service) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Preguntar([FromBody] PreguntaDto? pregunta, CancellationToken cancellationToken)
    {
        if (pregunta is null || string.IsNullOrWhiteSpace(pregunta.Texto))
            return BadRequest(new { respuesta = "Escribe tu consulta para poder ayudarte." });

        if (pregunta.Texto.Length > 500)
            return BadRequest(new { respuesta = "La consulta debe tener como máximo 500 caracteres." });

        var answer = await service.ResponderAsync(pregunta.Texto.Trim(), cancellationToken);
        return Json(new { respuesta = answer });
    }
}

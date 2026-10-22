using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestauranteApp.Services;

namespace RestauranteApp.Controllers;

// Contrato pequeño para recibir la pregunta del chat en formato JSON.
public record PreguntaDto([Required, StringLength(500)] string Texto);

// Limita las consultas de ambos chats; cada operación aplica su propia autorización.
[EnableRateLimiting("chat")]
[Route("[controller]/[action]")]
public class AsistenteController(AsistenteService service) : Controller
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Preguntar([FromBody] PreguntaDto? pregunta, CancellationToken cancellationToken)
    {
        var invalid = ValidarPregunta(pregunta, out var texto);
        if (invalid is not null) return invalid;

        var answer = await service.ResponderAsync(texto, cancellationToken);
        return Json(new { respuesta = answer });
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> PreguntarAdministrador([FromBody] PreguntaDto? pregunta, CancellationToken cancellationToken)
    {
        var invalid = ValidarPregunta(pregunta, out var texto);
        if (invalid is not null) return invalid;

        var answer = await service.ResponderAdministradorAsync(texto, cancellationToken);
        return Json(new { respuesta = answer });
    }

    private IActionResult? ValidarPregunta(PreguntaDto? pregunta, out string texto)
    {
        texto = pregunta?.Texto?.Trim() ?? "";
        if (texto.Length == 0)
            return BadRequest(new { respuesta = "Escribe tu consulta para poder ayudarte." });
        if (texto.Length > 500)
            return BadRequest(new { respuesta = "La consulta debe tener como máximo 500 caracteres." });

        return null;
    }
}

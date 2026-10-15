using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestauranteApp.Services;

namespace RestauranteApp.Controllers;

// Contrato pequeño para recibir la pregunta del chat en formato JSON.
public record PreguntaDto([Required, StringLength(500)] string Texto);

// Expone solo una operación de preguntas a visitantes anónimos. La política de rate limit "chat"
// limita solicitudes para reducir abuso y consumo del servicio que responde.
[AllowAnonymous]
[EnableRateLimiting("chat")]
[Route("[controller]/[action]")]
public class AsistenteController(AsistenteService service) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Preguntar([FromBody] PreguntaDto? pregunta, CancellationToken cancellationToken)
    {
        // Rechaza cuerpo ausente o texto vacío antes de invocar el servicio y generar una consulta innecesaria.
        if (pregunta is null || string.IsNullOrWhiteSpace(pregunta.Texto))
            return BadRequest(new { respuesta = "Escribe tu consulta para poder ayudarte." });

        // El atributo valida el DTO durante el model binding; esta comprobación conserva una respuesta JSON clara.
        if (pregunta.Texto.Length > 500)
            return BadRequest(new { respuesta = "La consulta debe tener como máximo 500 caracteres." });

        // Se recorta espacio exterior y se propaga la cancelación si el cliente cierra la solicitud.
        var answer = await service.ResponderAsync(pregunta.Texto.Trim(), cancellationToken);
        return Json(new { respuesta = answer });
    }
}

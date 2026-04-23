using FinMind.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinMind.Controllers;

[ApiController]
[Route("api/alertas")]
public class AlertasController : BaseController
{
    private readonly IAlertasService _alertasService;

    public AlertasController(IAlertasService alertasService)
    {
        _alertasService = alertasService;
    }

    [HttpGet("obtener")]
    [Authorize]
    public async Task<IActionResult> Obtener(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanyo = 20)
    {
        var usuarioId = ObtenerUsuarioId();
        var resultado = await _alertasService.ObtenerAsync(usuarioId, pagina, tamanyo);
        return Ok(resultado);
    }

    [HttpGet("no-leidas-total")]
    [Authorize]
    public async Task<IActionResult> ObtenerNoLeidasTotal()
    {
        var usuarioId = ObtenerUsuarioId();
        var total = await _alertasService.ObtenerNoLeidasTotalAsync(usuarioId);
        return Ok(new { total });
    }

    [HttpPatch("{id}/leer")]
    [Authorize]
    public async Task<IActionResult> MarcarLeida(Guid id)
    {
        var usuarioId = ObtenerUsuarioId();
        var existe = await _alertasService.MarcarLeidaAsync(id, usuarioId);

        if (!existe)
            return NotFound();

        return NoContent();
    }

    [HttpPatch("leer-todas")]
    [Authorize]
    public async Task<IActionResult> MarcarTodasLeidas()
    {
        var usuarioId = ObtenerUsuarioId();
        await _alertasService.MarcarTodasLeidasAsync(usuarioId);
        return NoContent();
    }
}

using FinMind.DTO;
using FinMind.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinMind.Controllers;

[ApiController]
[Route("api/configuracion-usuario")]
public class ConfiguracionUsuarioController : BaseController
{
    private readonly IConfiguracionUsuarioService _configuracionUsuarioService;

    public ConfiguracionUsuarioController(IConfiguracionUsuarioService configuracionUsuarioService)
    {
        _configuracionUsuarioService = configuracionUsuarioService;
    }

    [HttpGet("obtener")]
    [Authorize]
    public async Task<IActionResult> Obtener()
    {
        var usuarioId = ObtenerUsuarioId();
        var configuracion = await _configuracionUsuarioService.ObtenerAsync(usuarioId);
        return Ok(configuracion);
    }

    [HttpPatch("notificaciones")]
    [Authorize]
    public async Task<IActionResult> ActualizarNotificaciones([FromBody] ActualizarNotificacionesRequestDto request)
    {
        var usuarioId = ObtenerUsuarioId();
        var configuracion = await _configuracionUsuarioService.ActualizarNotificacionesAsync(usuarioId, request);
        return Ok(configuracion);
    }
}

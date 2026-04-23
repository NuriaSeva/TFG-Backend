using FinMind.DTO.Autenticacion;
using FinMind.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinMind.Controllers;

[ApiController]
[Route("api/autenticacion")]
public class AutenticacionController : ControllerBase
{
    private readonly IUsuarioService _usuariosServicio;

    public AutenticacionController(IUsuarioService usuariosServicio)
    {
        _usuariosServicio = usuariosServicio;
    }

    [HttpPost("registro")]
    [AllowAnonymous]
    public async Task<IActionResult> Registro([FromBody] RegistroUsuarioDto dto)
    {
        var respuesta = await _usuariosServicio.RegistrarAsync(dto);
        return Ok(respuesta);
    }

    [HttpPost("inicio-sesion")]
    [AllowAnonymous]
    public async Task<IActionResult> InicioSesion([FromBody] InicioSesionDto dto)
    {
        var respuesta = await _usuariosServicio.IniciarSesionAsync(dto);
        return Ok(respuesta);
    }

    [HttpPost("cambiar-password")]
    [Authorize]
    public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordDto dto)
    {
        var usuarioId = ObtenerUsuarioId();
        await _usuariosServicio.CambiarPasswordAsync(usuarioId, dto);
        return Ok(new { mensaje = "La contraseña se ha actualizado correctamente." });
    }

    [HttpGet("perfil")]
    [Authorize]
    public async Task<IActionResult> ObtenerPerfil()
    {
        var usuarioId = ObtenerUsuarioId();
        var perfil = await _usuariosServicio.ObtenerPerfilAsync(usuarioId);
        return Ok(perfil);
    }

    [HttpPut("perfil")]
    [Authorize]
    public async Task<IActionResult> ActualizarPerfil([FromBody] ActualizarPerfilUsuarioDto dto)
    {
        var usuarioId = ObtenerUsuarioId();
        var perfilActualizado = await _usuariosServicio.ActualizarPerfilAsync(usuarioId, dto);
        return Ok(perfilActualizado);
    }

    [HttpPost("cierre-sesion")]
    [Authorize]
    public IActionResult CierreSesion()
    {
        return Ok(new { mensaje = "Sesión cerrada correctamente." });
    }

    private Guid ObtenerUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var usuarioId))
        {
            throw new UnauthorizedAccessException("No se ha podido identificar al usuario autenticado.");
        }

        return usuarioId;
    }
}

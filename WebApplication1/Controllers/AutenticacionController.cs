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
        try
        {
            var respuesta = await _usuariosServicio.RegistrarAsync(dto);
            return Ok(respuesta);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpPost("inicio-sesion")]
    [AllowAnonymous]
    public async Task<IActionResult> InicioSesion([FromBody] InicioSesionDto dto)
    {
        try
        {
            var respuesta = await _usuariosServicio.IniciarSesionAsync(dto);
            return Ok(respuesta);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { mensaje = ex.Message });
        }
    }

    [HttpPost("cambiar-password")]
    [Authorize]
    public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordDto dto)
    {
        try
        {
            var usuarioId = ObtenerUsuarioId();
            await _usuariosServicio.CambiarPasswordAsync(usuarioId, dto);
            return Ok(new { mensaje = "La contraseña se ha actualizado correctamente." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
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

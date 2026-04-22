using FinMind.Data;
using FinMind.DTO;
using FinMind.Models.Enitdades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Controllers;

[ApiController]
[Route("api/configuracion-usuario")]
public class ConfiguracionUsuarioController : BaseController
{
    private readonly FinMindDbContext _context;

    public ConfiguracionUsuarioController(FinMindDbContext context)
    {
        _context = context;
    }

    [HttpGet("obtener")]
    [Authorize]
    public async Task<IActionResult> Obtener()
    {
        var usuarioId = ObtenerUsuarioId();
        var configuracion = await ObtenerOCrearConfiguracionAsync(usuarioId);

        return Ok(new ConfiguracionUsuarioResponseDto
        {
            NotificacionesActivas = configuracion.NotificacionesActivas,
            NotificacionesSoloCriticas = configuracion.NotificacionesSoloCriticas
        });
    }

    [HttpPatch("notificaciones")]
    [Authorize]
    public async Task<IActionResult> ActualizarNotificaciones([FromBody] ActualizarNotificacionesRequestDto request)
    {
        var usuarioId = ObtenerUsuarioId();
        var configuracion = await ObtenerOCrearConfiguracionAsync(usuarioId);

        configuracion.NotificacionesActivas = request.NotificacionesActivas;
        if (request.NotificacionesSoloCriticas.HasValue)
        {
            configuracion.NotificacionesSoloCriticas = request.NotificacionesSoloCriticas.Value;
        }
        configuracion.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new ConfiguracionUsuarioResponseDto
        {
            NotificacionesActivas = configuracion.NotificacionesActivas,
            NotificacionesSoloCriticas = configuracion.NotificacionesSoloCriticas
        });
    }

    private async Task<ConfiguracionUsuario> ObtenerOCrearConfiguracionAsync(Guid usuarioId)
    {
        var configuracion = await _context.ConfiguracionesUsuario
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

        if (configuracion != null)
            return configuracion;

        configuracion = new ConfiguracionUsuario
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            NotificacionesActivas = true,
            NotificacionesSoloCriticas = false,
            FechaActualizacion = DateTime.UtcNow
        };

        _context.ConfiguracionesUsuario.Add(configuracion);
        await _context.SaveChangesAsync();

        return configuracion;
    }
}

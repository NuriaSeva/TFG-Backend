using FinMind.Data;
using FinMind.DTO;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Services;

public class ConfiguracionUsuarioService : IConfiguracionUsuarioService
{
    private readonly FinMindDbContext _context;

    public ConfiguracionUsuarioService(FinMindDbContext context)
    {
        _context = context;
    }

    public async Task<ConfiguracionUsuarioResponseDto> ObtenerAsync(Guid usuarioId)
    {
        var configuracion = await ObtenerOCrearConfiguracionAsync(usuarioId);

        return new ConfiguracionUsuarioResponseDto
        {
            NotificacionesActivas = configuracion.NotificacionesActivas,
            NotificacionesSoloCriticas = configuracion.NotificacionesSoloCriticas
        };
    }

    public async Task<ConfiguracionUsuarioResponseDto> ActualizarNotificacionesAsync(Guid usuarioId, ActualizarNotificacionesRequestDto request)
    {
        var configuracion = await ObtenerOCrearConfiguracionAsync(usuarioId);

        configuracion.NotificacionesActivas = request.NotificacionesActivas;
        if (request.NotificacionesSoloCriticas.HasValue)
        {
            configuracion.NotificacionesSoloCriticas = request.NotificacionesSoloCriticas.Value;
        }
        configuracion.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new ConfiguracionUsuarioResponseDto
        {
            NotificacionesActivas = configuracion.NotificacionesActivas,
            NotificacionesSoloCriticas = configuracion.NotificacionesSoloCriticas
        };
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

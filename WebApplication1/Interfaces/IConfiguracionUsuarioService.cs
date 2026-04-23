using FinMind.DTO;

namespace FinMind.Interfaces;

public interface IConfiguracionUsuarioService
{
    Task<ConfiguracionUsuarioResponseDto> ObtenerAsync(Guid usuarioId);
    Task<ConfiguracionUsuarioResponseDto> ActualizarNotificacionesAsync(Guid usuarioId, ActualizarNotificacionesRequestDto request);
}

using FinMind.DTO.Autenticacion;

namespace FinMind.Services.Interfaces;

public interface IUsuarioService
{
    Task<AutenticacionResponseDto> RegistrarAsync(RegistroUsuarioDto dto);
    Task<AutenticacionResponseDto> IniciarSesionAsync(InicioSesionDto dto);
    Task CambiarPasswordAsync(Guid usuarioId, CambiarPasswordDto dto);
    Task<PerfilUsuarioResponseDto> ObtenerPerfilAsync(Guid usuarioId);
    Task<PerfilUsuarioResponseDto> ActualizarPerfilAsync(Guid usuarioId, ActualizarPerfilUsuarioDto dto);
}

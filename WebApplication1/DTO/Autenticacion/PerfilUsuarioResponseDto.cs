namespace FinMind.DTO.Autenticacion;

public class PerfilUsuarioResponseDto
{
    public string Email { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Apellidos { get; set; }
    public string MonedaPreferida { get; set; } = "EUR";
    public string Idioma { get; set; } = "es";
    public string Rol { get; set; } = string.Empty;
}

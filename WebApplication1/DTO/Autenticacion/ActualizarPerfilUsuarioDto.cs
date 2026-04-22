namespace FinMind.DTO.Autenticacion;

public class ActualizarPerfilUsuarioDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Apellidos { get; set; }
    public string MonedaPreferida { get; set; } = string.Empty;
    public string Idioma { get; set; } = string.Empty;
}

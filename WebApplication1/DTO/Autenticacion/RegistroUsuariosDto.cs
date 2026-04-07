namespace FinMind.DTO.Autenticacion;

public class RegistroUsuarioDto
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Apellidos { get; set; }
}
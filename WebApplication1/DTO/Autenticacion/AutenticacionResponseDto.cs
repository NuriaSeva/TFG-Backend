namespace FinMind.DTO.Autenticacion;

public class AutenticacionResponseDto
{
    public Guid UsuarioId { get; set; }
    public string Nombre { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Rol { get; set; } = null!;
    public bool DebeCambiarPassword { get; set; }
    public string Token { get; set; } = null!;
    public DateTime ExpiracionToken { get; set; }
}

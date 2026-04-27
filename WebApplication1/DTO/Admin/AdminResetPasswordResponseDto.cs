namespace FinMind.DTO.Admin;

public class AdminResetPasswordResponseDto
{
    public Guid UsuarioId { get; set; }
    public string PasswordTemporal { get; set; } = null!;
    public DateTime FechaGeneracionUtc { get; set; }
}

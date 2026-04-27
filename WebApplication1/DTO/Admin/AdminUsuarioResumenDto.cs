namespace FinMind.DTO.Admin;

public class AdminUsuarioResumenDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Apellidos { get; set; }
    public string Rol { get; set; } = null!;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaUltimoAcceso { get; set; }
}

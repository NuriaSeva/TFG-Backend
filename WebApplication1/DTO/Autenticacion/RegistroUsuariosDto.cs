using System.ComponentModel.DataAnnotations;

namespace FinMind.DTO.Autenticacion;

public class RegistroUsuarioDto
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = null!;

    [Required]
    [StringLength(80, MinimumLength = 2)]
    public string Nombre { get; set; } = null!;

    [StringLength(120)]
    public string? Apellidos { get; set; }
}

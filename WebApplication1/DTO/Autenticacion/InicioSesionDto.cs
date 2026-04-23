using System.ComponentModel.DataAnnotations;

namespace FinMind.DTO.Autenticacion;

public class InicioSesionDto
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = null!;
}

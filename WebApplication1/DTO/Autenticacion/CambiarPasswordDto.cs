using System.ComponentModel.DataAnnotations;

namespace FinMind.DTO.Autenticacion;

public class CambiarPasswordDto
{
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string PasswordActual { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string PasswordNueva { get; set; } = string.Empty;
}

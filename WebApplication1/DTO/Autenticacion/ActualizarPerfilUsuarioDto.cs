using System.ComponentModel.DataAnnotations;

namespace FinMind.DTO.Autenticacion;

public class ActualizarPerfilUsuarioDto
{
    [Required]
    [StringLength(80, MinimumLength = 2)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Apellidos { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string MonedaPreferida { get; set; } = string.Empty;

    [Required]
    [StringLength(5, MinimumLength = 2)]
    public string Idioma { get; set; } = string.Empty;
}

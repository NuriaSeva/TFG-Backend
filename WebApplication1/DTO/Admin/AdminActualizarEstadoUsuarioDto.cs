using System.ComponentModel.DataAnnotations;

namespace FinMind.DTO.Admin;

public class AdminActualizarEstadoUsuarioDto
{
    [Required]
    public bool Activo { get; set; }
}

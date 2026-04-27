namespace FinMind.DTO.Admin;

public class AdminUsuariosPaginadosResponseDto
{
    public IReadOnlyCollection<AdminUsuarioResumenDto> Usuarios { get; set; } = Array.Empty<AdminUsuarioResumenDto>();
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanoPagina { get; set; }
}

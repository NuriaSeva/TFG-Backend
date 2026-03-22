namespace FinMind.DTO.Banking;

public class GuardarCuentaDesdeReporteRequestDto
{
    public Guid UsuarioId { get; set; }
    public string ReportId { get; set; } = string.Empty;
}
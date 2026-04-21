namespace FinMind.DTO.Dashboard;

public class DashboardAlertaProactivaDto
{
    public string Tipo { get; set; } = string.Empty;
    public string Severidad { get; set; } = "media";
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}

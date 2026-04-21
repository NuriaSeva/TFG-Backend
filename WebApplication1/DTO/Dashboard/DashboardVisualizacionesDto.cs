namespace FinMind.DTO.Dashboard;

public class DashboardVisualizacionesDto
{
    public DashboardResumenDto ResumenMesActual { get; set; } = new();
    public List<DashboardCategoriaGastoDto> DistribucionGastosPorCategoria { get; set; } = new();
    public List<DashboardEvolucionMensualDto> EvolucionUltimosMeses { get; set; } = new();
    public List<DashboardAlertaProactivaDto> AlertasProactivas { get; set; } = new();
}

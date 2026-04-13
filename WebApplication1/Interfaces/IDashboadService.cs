using FinMind.DTO.Dashboard;

namespace FinMind.Interfaces;

public interface IDashboardService
{
    Task<DashboardResumenDto> ObtenerResumenMesActualAsync(Guid usuarioId);
    Task<DashboardVisualizacionesDto> ObtenerVisualizacionesAsync(Guid usuarioId);
    Task<DashboardMapaCalorDto> ObtenerMapaCalorMesActualAsync(Guid usuarioId);
}
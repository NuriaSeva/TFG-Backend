using FinMind.DTO.Dashboard;

namespace FinMind.Interfaces;

public interface IDashboardService
{
    Task<DashboardResumenDto> ObtenerResumenMesActualAsync(Guid usuarioId, int? mes = null, int? anio = null);
    Task<DashboardVisualizacionesDto> ObtenerVisualizacionesAsync(Guid usuarioId, int? mes = null, int? anio = null);
    Task<DashboardMapaCalorDto> ObtenerMapaCalorMesActualAsync(Guid usuarioId, int? mes = null, int? anio = null);
}
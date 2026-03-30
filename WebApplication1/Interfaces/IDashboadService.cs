using FinMind.DTO.Dashboard;

namespace FinMind.Interfaces;

public interface IDashboardService
{
    Task<DashboardResumenDto> ObtenerResumenMesActualAsync(Guid usuarioId);
}
using FinMind.DTO.Dashboard;

namespace FinMind.Interfaces;

public interface IAnaliticaPredictivaService
{
    List<DashboardAlertaProactivaDto> GenerarAlertasProactivas(
        DashboardResumenDto resumen,
        List<DashboardCategoriaGastoDto> distribucion,
        List<DashboardEvolucionMensualDto> evolucion,
        List<DashboardGastoDiaAnaliticaDto> gastosMesActualDetalle,
        List<DashboardGastoCategoriaMesAnaliticaDto> gastoCategoriaMensual,
        int mes,
        int anio);
}

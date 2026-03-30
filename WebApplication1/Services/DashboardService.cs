using FinMind.Data;
using FinMind.DTO.Dashboard;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Services;

public class DashboardService : IDashboardService
{
    private readonly FinMindDbContext _context;

    public DashboardService(FinMindDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardResumenDto> ObtenerResumenMesActualAsync(Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El id del usuario es obligatorio.", nameof(usuarioId));

        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var inicioMesSiguiente = inicioMes.AddMonths(1);

        var query = _context.Transacciones
            .AsNoTracking()
            .Where(t =>
                t.UsuarioId == usuarioId &&
                t.Fecha >= inicioMes &&
                t.Fecha < inicioMesSiguiente);

        var gastosMes = await query
            .Where(t => t.Tipo == TipoTransaccion.Gasto)
            .SumAsync(t => (decimal?)t.Importe) ?? 0m;

        var ingresosMes = await query
            .Where(t => t.Tipo == TipoTransaccion.Ingreso)
            .SumAsync(t => (decimal?)t.Importe) ?? 0m;

        var numeroGastosMes = await query
            .Where(t => t.Tipo == TipoTransaccion.Gasto)
            .CountAsync();

        var numeroIngresosMes = await query
            .Where(t => t.Tipo == TipoTransaccion.Ingreso)
            .CountAsync();

        return new DashboardResumenDto
        {
            GastosMes = gastosMes,
            IngresosMes = ingresosMes,
            NumeroGastosMes = numeroGastosMes,
            NumeroIngresosMes = numeroIngresosMes
        };
    }
}
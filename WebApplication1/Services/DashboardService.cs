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

    public async Task<DashboardVisualizacionesDto> ObtenerVisualizacionesAsync(Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El id del usuario es obligatorio.", nameof(usuarioId));

        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var inicioMesSiguiente = inicioMes.AddMonths(1);

        var resumen = await ObtenerResumenMesActualAsync(usuarioId);

        var gastosPorCategoriaRaw = await (
            from t in _context.Transacciones.AsNoTracking()
            join c in _context.Categorias.AsNoTracking()
                on t.CategoriaId equals c.Id into categoriasJoin
            from categoria in categoriasJoin.DefaultIfEmpty()
            where t.UsuarioId == usuarioId
                  && t.Fecha >= inicioMes
                  && t.Fecha < inicioMesSiguiente
                  && t.Tipo == TipoTransaccion.Gasto
            group t by (categoria != null ? categoria.Nombre : "Sin categoría") into g
            select new
            {
                Categoria = g.Key,
                Importe = g.Sum(x => x.Importe)
            })
            .OrderByDescending(x => x.Importe)
            .ToListAsync();

        var totalGastos = gastosPorCategoriaRaw.Sum(x => x.Importe);

        var distribucion = gastosPorCategoriaRaw
            .Select(x => new DashboardCategoriaGastoDto
            {
                Categoria = x.Categoria,
                Importe = x.Importe,
                Porcentaje = totalGastos <= 0 ? 0 : Math.Round((x.Importe / totalGastos) * 100m, 2)
            })
            .ToList();

        var inicioRango = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-5);
        var finRango = inicioMesSiguiente;

        var movimientosMensuales = await _context.Transacciones
            .AsNoTracking()
            .Where(t =>
                t.UsuarioId == usuarioId &&
                t.Fecha >= inicioRango &&
                t.Fecha < finRango)
            .GroupBy(t => new { t.Fecha.Year, t.Fecha.Month, t.Tipo })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Tipo,
                Total = g.Sum(x => x.Importe)
            })
            .ToListAsync();

        var evolucion = new List<DashboardEvolucionMensualDto>();

        for (var i = 0; i < 6; i++)
        {
            var fecha = inicioRango.AddMonths(i);

            var gastos = movimientosMensuales
                .Where(x => x.Year == fecha.Year &&
                            x.Month == fecha.Month &&
                            x.Tipo == TipoTransaccion.Gasto)
                .Sum(x => x.Total);

            var ingresos = movimientosMensuales
                .Where(x => x.Year == fecha.Year &&
                            x.Month == fecha.Month &&
                            x.Tipo == TipoTransaccion.Ingreso)
                .Sum(x => x.Total);

            evolucion.Add(new DashboardEvolucionMensualDto
            {
                Anio = fecha.Year,
                Mes = fecha.Month,
                Etiqueta = fecha.ToString("MMM yy", new System.Globalization.CultureInfo("es-ES")),
                Gastos = gastos,
                Ingresos = ingresos
            });
        }

        return new DashboardVisualizacionesDto
        {
            ResumenMesActual = resumen,
            DistribucionGastosPorCategoria = distribucion,
            EvolucionUltimosMeses = evolucion
        };
    }


    public async Task<DashboardMapaCalorDto> ObtenerMapaCalorMesActualAsync(Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El id del usuario es obligatorio.", nameof(usuarioId));

        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var inicioMesSiguiente = inicioMes.AddMonths(1);

        var gastosPorDia = await _context.Transacciones
            .AsNoTracking()
            .Where(t =>
                t.UsuarioId == usuarioId &&
                t.Tipo == TipoTransaccion.Gasto &&
                t.Fecha >= inicioMes &&
                t.Fecha < inicioMesSiguiente)
            .GroupBy(t => new
            {
                t.Fecha.Year,
                t.Fecha.Month,
                t.Fecha.Day
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                TotalGasto = g.Sum(x => x.Importe),
                NumeroMovimientos = g.Count()
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ThenBy(x => x.Day)
            .ToListAsync();

        var dias = gastosPorDia
            .Select(x => new DashboardMapaCalorDiaDto
            {
                Fecha = new DateTime(x.Year, x.Month, x.Day).ToString("yyyy-MM-dd"),
                TotalGasto = x.TotalGasto,
                NumeroMovimientos = x.NumeroMovimientos
            })
            .ToList();

        return new DashboardMapaCalorDto
        {
            Anio = hoy.Year,
            Mes = hoy.Month,
            MaximoGastoDia = dias.Count == 0 ? 0 : dias.Max(x => x.TotalGasto),
            Dias = dias
        };
    }
}
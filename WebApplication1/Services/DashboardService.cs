using FinMind.Data;
using FinMind.DTO.Dashboard;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace FinMind.Services;

public class DashboardService : IDashboardService
{
    private readonly FinMindDbContext _context;
    private readonly IAnaliticaPredictivaService _analiticaPredictivaService;

    public DashboardService(FinMindDbContext context, IAnaliticaPredictivaService analiticaPredictivaService)
    {
        _context = context;
        _analiticaPredictivaService = analiticaPredictivaService;
    }

    public async Task<DashboardResumenDto> ObtenerResumenMesActualAsync(Guid usuarioId, int? mes = null, int? anio = null)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El id del usuario es obligatorio.", nameof(usuarioId));

        var (anioFiltro, mesFiltro) = ResolverPeriodo(mes, anio);

        var inicioMes = new DateTime(anioFiltro, mesFiltro, 1);
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

    public async Task<DashboardVisualizacionesDto> ObtenerVisualizacionesAsync(Guid usuarioId, int? mes = null, int? anio = null)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El id del usuario es obligatorio.", nameof(usuarioId));

        var (anioFiltro, mesFiltro) = ResolverPeriodo(mes, anio);

        var inicioMes = new DateTime(anioFiltro, mesFiltro, 1);
        var inicioMesSiguiente = inicioMes.AddMonths(1);

        var resumen = await ObtenerResumenMesActualAsync(usuarioId, mesFiltro, anioFiltro);

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

        var inicioRango = inicioMes.AddMonths(-5);
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
                Etiqueta = fecha.ToString("MMM yy", new CultureInfo("es-ES")),
                Gastos = gastos,
                Ingresos = ingresos
            });
        }

        var gastosMesActualDetalleRaw = await _context.Transacciones
            .AsNoTracking()
            .Where(t =>
                t.UsuarioId == usuarioId &&
                t.Tipo == TipoTransaccion.Gasto &&
                t.Fecha >= inicioMes &&
                t.Fecha < inicioMesSiguiente)
            .Select(t => new
            {
                t.Fecha,
                t.Importe
            })
            .ToListAsync();

        var gastosMesActualDetalle = gastosMesActualDetalleRaw
            .Select(x => new DashboardGastoDiaAnaliticaDto(x.Fecha, x.Importe))
            .ToList();

        var inicioRangoPatrones = inicioMes.AddMonths(-3);

        var gastoCategoriaMensualRaw = await (
            from t in _context.Transacciones.AsNoTracking()
            join c in _context.Categorias.AsNoTracking()
                on t.CategoriaId equals c.Id into categoriasJoin
            from categoria in categoriasJoin.DefaultIfEmpty()
            where t.UsuarioId == usuarioId
                  && t.Tipo == TipoTransaccion.Gasto
                  && t.Fecha >= inicioRangoPatrones
                  && t.Fecha < inicioMesSiguiente
            group t by new
            {
                t.Fecha.Year,
                t.Fecha.Month,
                Categoria = categoria != null ? categoria.Nombre : "Sin categoría"
            }
            into g
            select new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Categoria,
                Importe = g.Sum(x => x.Importe)
            })
            .ToListAsync();

        var gastoCategoriaMensual = gastoCategoriaMensualRaw
            .Select(x => new DashboardGastoCategoriaMesAnaliticaDto(x.Year, x.Month, x.Categoria, x.Importe))
            .ToList();

        var notificacionesActivas = await SonNotificacionesActivasAsync(usuarioId);
        var alertasProactivas = notificacionesActivas
            ? _analiticaPredictivaService.GenerarAlertasProactivas(
                resumen,
                distribucion,
                evolucion,
                gastosMesActualDetalle,
                gastoCategoriaMensual,
                mesFiltro,
                anioFiltro)
            : new List<DashboardAlertaProactivaDto>();

        if (notificacionesActivas)
        {
            await PersistirAlertasProactivasAsync(usuarioId, alertasProactivas, mesFiltro, anioFiltro);
        }

        return new DashboardVisualizacionesDto
        {
            ResumenMesActual = resumen,
            DistribucionGastosPorCategoria = distribucion,
            EvolucionUltimosMeses = evolucion,
            AlertasProactivas = alertasProactivas
        };
    }

    public async Task<DashboardMapaCalorDto> ObtenerMapaCalorMesActualAsync(Guid usuarioId, int? mes = null, int? anio = null)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El id del usuario es obligatorio.", nameof(usuarioId));

        var (anioFiltro, mesFiltro) = ResolverPeriodo(mes, anio);

        var inicioMes = new DateTime(anioFiltro, mesFiltro, 1);
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
            Anio = anioFiltro,
            Mes = mesFiltro,
            MaximoGastoDia = dias.Count == 0 ? 0 : dias.Max(x => x.TotalGasto),
            Dias = dias
        };
    }

    private static (int anio, int mes) ResolverPeriodo(int? mes, int? anio)
    {
        var hoy = DateTime.Today;

        var mesFinal = mes ?? hoy.Month;
        var anioFinal = anio ?? hoy.Year;

        if (mesFinal < 1 || mesFinal > 12)
            throw new ArgumentOutOfRangeException(nameof(mes), "El mes debe estar entre 1 y 12.");

        if (anioFinal < 2000 || anioFinal > 2100)
            throw new ArgumentOutOfRangeException(nameof(anio), "El año no es válido.");

        return (anioFinal, mesFinal);
    }

    private async Task PersistirAlertasProactivasAsync(
        Guid usuarioId,
        List<DashboardAlertaProactivaDto> alertas,
        int mes,
        int anio)
    {
        if (alertas.Count == 0)
            return;

        var inicioMes = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var inicioMesSiguiente = inicioMes.AddMonths(1);
        var huboCambios = false;
        var soloCriticas = await SonNotificacionesSoloCriticasAsync(usuarioId);

        var alertasMes = await _context.Alertas
            .Where(a =>
                a.UsuarioId == usuarioId &&
                a.FechaCreacion >= inicioMes &&
                a.FechaCreacion < inicioMesSiguiente)
            .ToListAsync();

        var duplicados = alertasMes
            .GroupBy(a => new { a.Tipo, a.Titulo })
            .SelectMany(g => g
                .OrderByDescending(a => a.FechaCreacion)
                .ThenByDescending(a => a.Id)
                .Skip(1))
            .ToList();

        if (duplicados.Count > 0)
        {
            _context.Alertas.RemoveRange(duplicados);
            huboCambios = true;
        }

        foreach (var alerta in alertas)
        {
            var esInsightSoloInformes = alerta.Tipo is "gasto-inusual" or "patron-semanal" or "patron-categoria" or "patron-dia-semana";
            if (esInsightSoloInformes)
                continue;

            var tipoAlerta = alerta.Tipo switch
            {
                "prediccion" => TipoAlerta.Prediccion,
                "gasto-inusual" => TipoAlerta.GastoInusual,
                "concentracion" => TipoAlerta.Informativa,
                "patron-semanal" => TipoAlerta.Informativa,
                "patron-categoria" => TipoAlerta.Informativa,
                "patron-dia-semana" => TipoAlerta.Informativa,
                _ => TipoAlerta.Informativa
            };

            if (soloCriticas && !EsAlertaCritica(tipoAlerta))
                continue;

            var yaExiste = alertasMes.Any(a =>
                a.Tipo == tipoAlerta &&
                a.Titulo == alerta.Titulo);

            if (yaExiste)
                continue;

            var nuevaAlerta = new Alerta
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                Tipo = tipoAlerta,
                Titulo = alerta.Titulo,
                Mensaje = alerta.Mensaje,
                Leida = false,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Alertas.Add(nuevaAlerta);
            alertasMes.Add(nuevaAlerta);

            huboCambios = true;
        }

        if (huboCambios)
        {
            await _context.SaveChangesAsync();
        }
    }

    private async Task<bool> SonNotificacionesActivasAsync(Guid usuarioId)
    {
        var configuracion = await _context.ConfiguracionesUsuario
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

        return configuracion?.NotificacionesActivas ?? true;
    }

    private async Task<bool> SonNotificacionesSoloCriticasAsync(Guid usuarioId)
    {
        var configuracion = await _context.ConfiguracionesUsuario
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

        return configuracion?.NotificacionesSoloCriticas ?? false;
    }

    private static bool EsAlertaCritica(TipoAlerta tipo)
    {
        return tipo == TipoAlerta.Prediccion ||
               tipo == TipoAlerta.PresupuestoSuperado ||
               tipo == TipoAlerta.ErrorSincronizacion;
    }
}

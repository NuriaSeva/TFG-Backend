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

    public DashboardService(FinMindDbContext context)
    {
        _context = context;
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

        var notificacionesActivas = await SonNotificacionesActivasAsync(usuarioId);
        var alertasProactivas = notificacionesActivas
            ? GenerarAlertasProactivas(
                resumen,
                distribucion,
                evolucion,
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

    private static List<DashboardAlertaProactivaDto> GenerarAlertasProactivas(
        DashboardResumenDto resumen,
        List<DashboardCategoriaGastoDto> distribucion,
        List<DashboardEvolucionMensualDto> evolucion,
        int mes,
        int anio)
    {
        var alertas = new List<DashboardAlertaProactivaDto>();
        var hoy = DateTime.Today;
        var esMesActual = hoy.Month == mes && hoy.Year == anio;

        if (esMesActual && resumen.IngresosMes > 0 && resumen.GastosMes > resumen.IngresosMes * 1.10m)
        {
            var exceso = resumen.GastosMes - resumen.IngresosMes;
            alertas.Add(new DashboardAlertaProactivaDto
            {
                Tipo = "prediccion",
                Severidad = "alta",
                Titulo = "Riesgo de cierre en negativo",
                Mensaje = $"Tus gastos superan a los ingresos en {Math.Round(exceso, 2):N2} EUR este mes."
            });
        }

        var mesActualSerie = evolucion
            .FirstOrDefault(e => e.Mes == mes && e.Anio == anio);

        var historico3 = evolucion
            .Where(e => !(e.Mes == mes && e.Anio == anio))
            .TakeLast(3)
            .ToList();

        if (esMesActual && mesActualSerie != null && historico3.Count >= 3)
        {
            var mediaGasto3Meses = historico3.Average(e => e.Gastos);
            if (mediaGasto3Meses > 0)
            {
                var diasMes = DateTime.DaysInMonth(anio, mes);
                var diaActual = Math.Min(hoy.Day, diasMes);
                var gastoProyectado = (mesActualSerie.Gastos / Math.Max(diaActual, 1)) * diasMes;

                if (gastoProyectado > mediaGasto3Meses * 1.20m)
                {
                    var incremento = ((gastoProyectado / mediaGasto3Meses) - 1m) * 100m;
                    alertas.Add(new DashboardAlertaProactivaDto
                    {
                        Tipo = "gasto-inusual",
                        Severidad = "media",
                        Titulo = "Ritmo de gasto por encima de lo habitual",
                        Mensaje = $"Si mantienes este ritmo, cerrarías alrededor de un {Math.Round(incremento, 1):N1}% por encima de tu media de 3 meses."
                    });
                }
            }
        }

        var categoriaPrincipal = distribucion
            .OrderByDescending(d => d.Porcentaje)
            .FirstOrDefault();

        if (categoriaPrincipal != null && categoriaPrincipal.Porcentaje >= 45m && resumen.GastosMes >= 100m)
        {
            alertas.Add(new DashboardAlertaProactivaDto
            {
                Tipo = "concentracion",
                Severidad = "baja",
                Titulo = "Concentración de gasto detectada",
                Mensaje = $"La categoría \"{categoriaPrincipal.Categoria}\" representa el {categoriaPrincipal.Porcentaje:N1}% de tus gastos del mes."
            });
        }

        return alertas;
    }

    private async Task PersistirAlertasProactivasAsync(
        Guid usuarioId,
        List<DashboardAlertaProactivaDto> alertas,
        int mes,
        int anio)
    {
        if (alertas.Count == 0)
            return;

        var inicioMes = new DateTime(anio, mes, 1);
        var inicioMesSiguiente = inicioMes.AddMonths(1);
        var huboCambios = false;

        foreach (var alerta in alertas)
        {
            var tipoAlerta = alerta.Tipo switch
            {
                "prediccion" => TipoAlerta.Prediccion,
                "gasto-inusual" => TipoAlerta.GastoInusual,
                "concentracion" => TipoAlerta.Informativa,
                _ => TipoAlerta.Informativa
            };

            var yaExiste = await _context.Alertas.AnyAsync(a =>
                a.UsuarioId == usuarioId &&
                a.Tipo == tipoAlerta &&
                a.Titulo == alerta.Titulo &&
                a.Mensaje == alerta.Mensaje &&
                a.FechaCreacion >= inicioMes &&
                a.FechaCreacion < inicioMesSiguiente);

            if (yaExiste)
                continue;

            _context.Alertas.Add(new Alerta
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                Tipo = tipoAlerta,
                Titulo = alerta.Titulo,
                Mensaje = alerta.Mensaje,
                Leida = false,
                FechaCreacion = DateTime.UtcNow
            });

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
}

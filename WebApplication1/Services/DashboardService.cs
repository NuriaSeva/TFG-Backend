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
            .Select(x => new GastoDiaAnalitica(x.Fecha, x.Importe))
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
            .Select(x => new GastoCategoriaMesAnalitica(x.Year, x.Month, x.Categoria, x.Importe))
            .ToList();

        var notificacionesActivas = await SonNotificacionesActivasAsync(usuarioId);
        var alertasProactivas = notificacionesActivas
            ? GenerarAlertasProactivas(
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

    private static List<DashboardAlertaProactivaDto> GenerarAlertasProactivas(
        DashboardResumenDto resumen,
        List<DashboardCategoriaGastoDto> distribucion,
        List<DashboardEvolucionMensualDto> evolucion,
        List<GastoDiaAnalitica> gastosMesActualDetalle,
        List<GastoCategoriaMesAnalitica> gastoCategoriaMensual,
        int mes,
        int anio)
    {
        var alertas = new List<DashboardAlertaProactivaDto>();
        var hoy = DateTime.Today;
        var esMesActual = hoy.Month == mes && hoy.Year == anio;

        var gastoProyectadoRegresion = esMesActual
            ? CalcularPrediccionGastoFinMesRegresion(gastosMesActualDetalle, anio, mes, hoy.Day)
            : null;

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
                var gastoProyectado = gastoProyectadoRegresion
                    ?? (mesActualSerie.Gastos / Math.Max(diaActual, 1)) * diasMes;

                var incremento = ((gastoProyectado / mediaGasto3Meses) - 1m) * 100m;

                if (gastoProyectadoRegresion.HasValue && gastoProyectadoRegresion.Value >= resumen.GastosMes * 1.08m)
                {
                    alertas.Add(new DashboardAlertaProactivaDto
                    {
                        Tipo = "prediccion",
                        Severidad = incremento >= 30m ? "alta" : "media",
                        Titulo = "Predicción de cierre de gasto",
                        Mensaje = $"Con tu tendencia actual, el gasto estimado al cierre del mes es de {Math.Round(gastoProyectadoRegresion.Value, 2):N2} EUR."
                    });
                }

                if (gastoProyectado > mediaGasto3Meses * 1.20m)
                {
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

        if (esMesActual && resumen.IngresosMes > 0)
        {
            var gastosReferenciaRiesgo = gastoProyectadoRegresion ?? resumen.GastosMes;
            if (gastosReferenciaRiesgo > resumen.IngresosMes * 1.10m)
            {
                var exceso = gastosReferenciaRiesgo - resumen.IngresosMes;
                alertas.Add(new DashboardAlertaProactivaDto
                {
                    Tipo = "prediccion",
                    Severidad = "alta",
                    Titulo = "Riesgo de cierre en negativo",
                    Mensaje = $"Con el ritmo actual, podrías cerrar el mes con {Math.Round(exceso, 2):N2} EUR de gasto por encima de tus ingresos."
                });
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

        AgregarPatronFinDeSemana(alertas, gastosMesActualDetalle, mes, anio);
        AgregarPatronDiaSemanaPrincipal(alertas, gastosMesActualDetalle);
        AgregarPatronCategoriaRecurrente(alertas, gastoCategoriaMensual, mes, anio);

        return alertas;
    }

    private static decimal? CalcularPrediccionGastoFinMesRegresion(
        List<GastoDiaAnalitica> gastosMesActualDetalle,
        int anio,
        int mes,
        int diaActual)
    {
        if (gastosMesActualDetalle.Count == 0)
            return null;

        var diasMes = DateTime.DaysInMonth(anio, mes);
        var ultimoDiaConDatos = Math.Min(Math.Max(diaActual, 1), diasMes);

        // Serie de gasto acumulado diario (incluye días sin gasto).
        var gastoPorDia = gastosMesActualDetalle
            .GroupBy(x => x.Fecha.Day)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Importe));

        var x = new List<double>();
        var y = new List<double>();
        decimal acumulado = 0m;

        for (var dia = 1; dia <= ultimoDiaConDatos; dia++)
        {
            acumulado += gastoPorDia.TryGetValue(dia, out var gastoDia) ? gastoDia : 0m;
            x.Add(dia);
            y.Add((double)acumulado);
        }

        if (x.Count < 7)
            return null;

        var n = x.Count;
        var sumaX = x.Sum();
        var sumaY = y.Sum();
        var sumaXY = x.Zip(y, (xi, yi) => xi * yi).Sum();
        var sumaXX = x.Sum(v => v * v);

        var denominador = (n * sumaXX) - (sumaX * sumaX);
        if (Math.Abs(denominador) < 0.00001d)
            return null;

        var pendiente = ((n * sumaXY) - (sumaX * sumaY)) / denominador;
        var intercepto = (sumaY - (pendiente * sumaX)) / n;

        var prediccion = intercepto + (pendiente * diasMes);
        var gastoActual = acumulado;

        // Guardarraíles para evitar estimaciones no realistas.
        var prediccionDecimal = Math.Max((decimal)prediccion, gastoActual);
        prediccionDecimal = Math.Max(prediccionDecimal, 0m);

        return Math.Round(prediccionDecimal, 2);
    }

    private static void AgregarPatronFinDeSemana(
        List<DashboardAlertaProactivaDto> alertas,
        List<GastoDiaAnalitica> gastosMesActualDetalle,
        int mes,
        int anio)
    {
        if (gastosMesActualDetalle.Count < 8)
            return;

        var finDeSemana = gastosMesActualDetalle
            .Where(x => x.Fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            .ToList();

        var laborables = gastosMesActualDetalle
            .Where(x => x.Fecha.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .ToList();

        if (finDeSemana.Count < 3 || laborables.Count < 3)
            return;

        var mediaFinDeSemana = finDeSemana.Average(x => x.Importe);
        var mediaLaborables = laborables.Average(x => x.Importe);

        if (mediaLaborables <= 0)
            return;

        var ratio = mediaFinDeSemana / mediaLaborables;
        if (ratio < 1.35m)
            return;

        var incremento = (ratio - 1m) * 100m;
        alertas.Add(new DashboardAlertaProactivaDto
        {
            Tipo = "patron-semanal",
            Severidad = "baja",
            Titulo = "Sueles gastar más en fin de semana",
            Mensaje = $"Este mes, tu gasto medio en sábado y domingo es un {Math.Round(incremento, 1):N1}% superior al de días laborables."
        });
    }

    private static void AgregarPatronCategoriaRecurrente(
        List<DashboardAlertaProactivaDto> alertas,
        List<GastoCategoriaMesAnalitica> gastoCategoriaMensual,
        int mes,
        int anio)
    {
        if (gastoCategoriaMensual.Count == 0)
            return;

        var inicioMes = new DateTime(anio, mes, 1);
        var mesesAnalizados = Enumerable
            .Range(0, 4)
            .Select(offset => inicioMes.AddMonths(-offset))
            .ToList();

        var topPorMes = new List<(int Anio, int Mes, string Categoria, decimal Importe, decimal TotalMes)>();

        foreach (var mesAnalizado in mesesAnalizados)
        {
            var itemsMes = gastoCategoriaMensual
                .Where(x => x.Anio == mesAnalizado.Year && x.Mes == mesAnalizado.Month)
                .ToList();

            if (itemsMes.Count == 0)
                continue;

            var totalMes = itemsMes.Sum(x => x.Importe);
            if (totalMes <= 0)
                continue;

            var topMes = itemsMes
                .OrderByDescending(x => x.Importe)
                .First();

            topPorMes.Add((mesAnalizado.Year, mesAnalizado.Month, topMes.Categoria, topMes.Importe, totalMes));
        }

        if (topPorMes.Count < 3)
            return;

        var topMesActual = topPorMes.FirstOrDefault(x => x.Anio == anio && x.Mes == mes);
        if (string.IsNullOrWhiteSpace(topMesActual.Categoria))
            return;

        var repeticiones = topPorMes.Count(x => x.Categoria == topMesActual.Categoria);
        var porcentajeMesActual = topMesActual.TotalMes <= 0
            ? 0
            : (topMesActual.Importe / topMesActual.TotalMes) * 100m;

        if (repeticiones < 3 || porcentajeMesActual < 35m)
            return;

        alertas.Add(new DashboardAlertaProactivaDto
        {
            Tipo = "patron-categoria",
            Severidad = "baja",
            Titulo = $"Sueles gastar más en {topMesActual.Categoria}",
            Mensaje = $"\"{topMesActual.Categoria}\" es tu categoría principal en {repeticiones} de los últimos {topPorMes.Count} meses y representa el {porcentajeMesActual:N1}% del gasto de este mes."
        });
    }

    private static void AgregarPatronDiaSemanaPrincipal(
        List<DashboardAlertaProactivaDto> alertas,
        List<GastoDiaAnalitica> gastosMesActualDetalle)
    {
        if (gastosMesActualDetalle.Count < 10)
            return;

        var gastosPorDiaSemana = gastosMesActualDetalle
            .GroupBy(x => x.Fecha.DayOfWeek)
            .Select(g => new
            {
                DiaSemana = g.Key,
                GastoTotal = g.Sum(x => x.Importe),
                Media = g.Average(x => x.Importe),
                Conteo = g.Count()
            })
            .Where(x => x.Conteo >= 2)
            .OrderByDescending(x => x.Media)
            .ToList();

        if (gastosPorDiaSemana.Count < 2)
            return;

        var principal = gastosPorDiaSemana[0];
        var segunda = gastosPorDiaSemana[1];

        if (segunda.Media <= 0)
            return;

        var diferencia = ((principal.Media / segunda.Media) - 1m) * 100m;
        if (diferencia < 12m)
            return;

        alertas.Add(new DashboardAlertaProactivaDto
        {
            Tipo = "patron-dia-semana",
            Severidad = "baja",
            Titulo = $"Sueles gastar más los {NombreDiaSemanaEs(principal.DiaSemana)}",
            Mensaje = $"Tu gasto medio en {NombreDiaSemanaEs(principal.DiaSemana)} es un {diferencia:N1}% superior al del siguiente día con mayor gasto."
        });
    }

    private static string NombreDiaSemanaEs(DayOfWeek dia)
    {
        return dia switch
        {
            DayOfWeek.Monday => "lunes",
            DayOfWeek.Tuesday => "martes",
            DayOfWeek.Wednesday => "miércoles",
            DayOfWeek.Thursday => "jueves",
            DayOfWeek.Friday => "viernes",
            DayOfWeek.Saturday => "sábados",
            DayOfWeek.Sunday => "domingos",
            _ => "días"
        };
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

        // Limpia duplicados históricos por tipo+título dentro del mismo mes.
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

    private sealed record GastoDiaAnalitica(DateTime Fecha, decimal Importe);

    private sealed record GastoCategoriaMesAnalitica(int Anio, int Mes, string Categoria, decimal Importe);
}

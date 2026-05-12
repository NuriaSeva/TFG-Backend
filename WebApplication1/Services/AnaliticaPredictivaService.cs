using FinMind.DTO.Dashboard;
using FinMind.Interfaces;
using FinMind.Models;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace FinMind.Services;

public class AnaliticaPredictivaService : IAnaliticaPredictivaService
{
    private readonly string? _contentRootPath;
    private readonly IAOptions _options;
    private readonly MLContext _mlContext = new(seed: 12);
    private readonly SemaphoreSlim _modeloPrediccionSemaphore = new(1, 1);
    private ITransformer? _modeloPrediccionGasto;

    public AnaliticaPredictivaService()
    {
        _options = new IAOptions();
    }

    public AnaliticaPredictivaService(IWebHostEnvironment environment, IOptions<IAOptions> options)
    {
        _contentRootPath = environment.ContentRootPath;
        _options = options.Value;
    }

    public List<DashboardAlertaProactivaDto> GenerarAlertasProactivas(
        DashboardResumenDto resumen,
        List<DashboardCategoriaGastoDto> distribucion,
        List<DashboardEvolucionMensualDto> evolucion,
        List<DashboardGastoDiaAnaliticaDto> gastosMesActualDetalle,
        List<DashboardGastoCategoriaMesAnaliticaDto> gastoCategoriaMensual,
        int mes,
        int anio)
    {
        var alertas = new List<DashboardAlertaProactivaDto>();
        var hoy = DateTime.Today;
        var esMesActual = hoy.Month == mes && hoy.Year == anio;

        var mesActualSerie = evolucion
            .FirstOrDefault(e => e.Mes == mes && e.Anio == anio);

        var historico3 = evolucion
            .Where(e => !(e.Mes == mes && e.Anio == anio))
            .TakeLast(3)
            .ToList();

        decimal? gastoProyectadoModelo = null;

        if (esMesActual && mesActualSerie != null && historico3.Count >= 3)
        {
            var mediaGasto3Meses = historico3.Average(e => e.Gastos);
            if (mediaGasto3Meses > 0)
            {
                var diasMes = DateTime.DaysInMonth(anio, mes);
                var diaActual = Math.Min(hoy.Day, diasMes);
                gastoProyectadoModelo = PredecirGastoCierreConModelo(
                    diaActual,
                    diasMes,
                    mesActualSerie.Gastos,
                    resumen.IngresosMes,
                    mediaGasto3Meses,
                    mes);
                if (!gastoProyectadoModelo.HasValue)
                    return AgregarAlertasNoPredictivas(alertas, resumen, distribucion, gastosMesActualDetalle, gastoCategoriaMensual, mes, anio);

                var gastoProyectado = gastoProyectadoModelo.Value;

                var incremento = ((gastoProyectado / mediaGasto3Meses) - 1m) * 100m;

                if (gastoProyectado >= resumen.GastosMes * 1.08m)
                {
                    alertas.Add(new DashboardAlertaProactivaDto
                    {
                        Tipo = "prediccion",
                        Severidad = incremento >= 30m ? "alta" : "media",
                        Titulo = "Predicción de cierre de gasto",
                        Mensaje = $"Segun tu tendencia actual y tu historial reciente, el gasto estimado al cierre del mes es de {Math.Round(gastoProyectado, 2):N2} EUR."
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
            var gastosReferenciaRiesgo = gastoProyectadoModelo;
            if (!gastosReferenciaRiesgo.HasValue)
                return AgregarAlertasNoPredictivas(alertas, resumen, distribucion, gastosMesActualDetalle, gastoCategoriaMensual, mes, anio);

            if (gastosReferenciaRiesgo.Value > resumen.IngresosMes * 1.10m)
            {
                var exceso = gastosReferenciaRiesgo.Value - resumen.IngresosMes;
                alertas.Add(new DashboardAlertaProactivaDto
                {
                    Tipo = "prediccion",
                    Severidad = "alta",
                    Titulo = "Riesgo de cierre en negativo",
                    Mensaje = $"Con el ritmo actual, podrías cerrar el mes con {Math.Round(exceso, 2):N2} EUR de gasto por encima de tus ingresos."
                });
            }
        }

        return AgregarAlertasNoPredictivas(alertas, resumen, distribucion, gastosMesActualDetalle, gastoCategoriaMensual, mes, anio);
    }

    private static List<DashboardAlertaProactivaDto> AgregarAlertasNoPredictivas(
        List<DashboardAlertaProactivaDto> alertas,
        DashboardResumenDto resumen,
        List<DashboardCategoriaGastoDto> distribucion,
        List<DashboardGastoDiaAnaliticaDto> gastosMesActualDetalle,
        List<DashboardGastoCategoriaMesAnaliticaDto> gastoCategoriaMensual,
        int mes,
        int anio)
    {
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

        AgregarPatronFinDeSemana(alertas, gastosMesActualDetalle);
        AgregarPatronDiaSemanaPrincipal(alertas, gastosMesActualDetalle);
        AgregarPatronCategoriaRecurrente(alertas, gastoCategoriaMensual, mes, anio);

        return alertas;
    }

    private decimal? PredecirGastoCierreConModelo(
        int diaActual,
        int diasMes,
        decimal gastoAcumulado,
        decimal ingresosMes,
        decimal mediaHistorica3Meses,
        int mes)
    {
        if (!_options.Enabled)
            return null;

        var modelo = ObtenerModeloPrediccionGasto();
        if (modelo == null)
            return null;

        var input = CrearInputPrediccionGasto(
            diaActual,
            diasMes,
            gastoAcumulado,
            ingresosMes,
            mediaHistorica3Meses,
            mes);

        var engine = _mlContext.Model.CreatePredictionEngine<PrediccionGastoTrainingInput, PrediccionGastoPrediction>(modelo);
        var output = engine.Predict(input);
        if (float.IsNaN(output.Score) || float.IsInfinity(output.Score) || output.Score <= 0)
            return null;

        var prediccion = Math.Max((decimal)output.Score, gastoAcumulado);
        return Math.Round(prediccion, 2);
    }

    private ITransformer? ObtenerModeloPrediccionGasto()
    {
        if (_modeloPrediccionGasto != null)
            return _modeloPrediccionGasto;

        _modeloPrediccionSemaphore.Wait();
        try
        {
            if (_modeloPrediccionGasto != null)
                return _modeloPrediccionGasto;

            var modelPath = ResolverRuta(Path.Combine(_options.ModelOutputPath, _options.PrediccionGastoModelFileName));
            if (File.Exists(modelPath))
            {
                using var stream = File.OpenRead(modelPath);
                _modeloPrediccionGasto = _mlContext.Model.Load(stream, out _);
                return _modeloPrediccionGasto;
            }

            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            _modeloPrediccionSemaphore.Release();
        }
    }

    private static PrediccionGastoTrainingInput CrearInputPrediccionGasto(
        int diaActual,
        int diasMes,
        decimal gastoAcumulado,
        decimal ingresosMes,
        decimal mediaHistorica3Meses,
        int mes)
    {
        var diasSeguros = Math.Max(diasMes, 1);
        var diaSeguro = Math.Clamp(diaActual, 1, diasSeguros);

        return new PrediccionGastoTrainingInput
        {
            DiaMes = diaSeguro,
            DiasMes = diasSeguros,
            PorcentajeMesTranscurrido = diaSeguro / (float)diasSeguros,
            GastoAcumulado = (float)gastoAcumulado,
            IngresosMes = (float)ingresosMes,
            MediaGasto3Meses = (float)mediaHistorica3Meses,
            GastoMedioDiarioActual = (float)(gastoAcumulado / Math.Max(diaSeguro, 1)),
            Mes = mes
        };
    }

    private string ResolverRuta(string relativeOrAbsolute)
    {
        if (Path.IsPathRooted(relativeOrAbsolute))
            return relativeOrAbsolute;

        return Path.Combine(_contentRootPath ?? AppContext.BaseDirectory, relativeOrAbsolute);
    }

    private static void AgregarPatronFinDeSemana(
        List<DashboardAlertaProactivaDto> alertas,
        List<DashboardGastoDiaAnaliticaDto> gastosMesActualDetalle)
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
        List<DashboardGastoCategoriaMesAnaliticaDto> gastoCategoriaMensual,
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
        List<DashboardGastoDiaAnaliticaDto> gastosMesActualDetalle)
    {
        if (gastosMesActualDetalle.Count < 10)
            return;

        var gastosPorDiaSemana = gastosMesActualDetalle
            .GroupBy(x => x.Fecha.DayOfWeek)
            .Select(g => new
            {
                DiaSemana = g.Key,
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

    private sealed class PrediccionGastoTrainingInput
    {
        [LoadColumn(0)]
        public float DiaMes { get; set; }

        [LoadColumn(1)]
        public float DiasMes { get; set; }

        [LoadColumn(2)]
        public float PorcentajeMesTranscurrido { get; set; }

        [LoadColumn(3)]
        public float GastoAcumulado { get; set; }

        [LoadColumn(4)]
        public float IngresosMes { get; set; }

        [LoadColumn(5)]
        public float MediaGasto3Meses { get; set; }

        [LoadColumn(6)]
        public float GastoMedioDiarioActual { get; set; }

        [LoadColumn(7)]
        public float Mes { get; set; }

        [LoadColumn(8)]
        public float GastoFinalMes { get; set; }
    }

    private sealed class PrediccionGastoPrediction
    {
        public float Score { get; set; }
    }
}

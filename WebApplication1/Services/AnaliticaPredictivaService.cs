using FinMind.DTO.Dashboard;
using FinMind.Interfaces;

namespace FinMind.Services;

public class AnaliticaPredictivaService : IAnaliticaPredictivaService
{
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

        AgregarPatronFinDeSemana(alertas, gastosMesActualDetalle);
        AgregarPatronDiaSemanaPrincipal(alertas, gastosMesActualDetalle);
        AgregarPatronCategoriaRecurrente(alertas, gastoCategoriaMensual, mes, anio);

        return alertas;
    }

    private static decimal? CalcularPrediccionGastoFinMesRegresion(
        List<DashboardGastoDiaAnaliticaDto> gastosMesActualDetalle,
        int anio,
        int mes,
        int diaActual)
    {
        if (gastosMesActualDetalle.Count == 0)
            return null;

        var diasMes = DateTime.DaysInMonth(anio, mes);
        var ultimoDiaConDatos = Math.Min(Math.Max(diaActual, 1), diasMes);

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

        var prediccionDecimal = Math.Max((decimal)prediccion, gastoActual);
        prediccionDecimal = Math.Max(prediccionDecimal, 0m);

        return Math.Round(prediccionDecimal, 2);
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
}

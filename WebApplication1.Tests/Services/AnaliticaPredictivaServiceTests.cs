using FinMind.DTO.Dashboard;
using FinMind.Services;

namespace FinMind.Tests.Services;

public class AnaliticaPredictivaServiceTests
{
    [Fact]
    public void GenerarAlertasProactivas_MesActual_GeneraAlertaPrediccion()
    {
        var servicio = new AnaliticaPredictivaService();
        var hoy = DateTime.Today;

        var resumen = new DashboardResumenDto
        {
            GastosMes = 300m,
            IngresosMes = 1200m
        };

        var distribucion = new List<DashboardCategoriaGastoDto>
        {
            new() { Categoria = "Supermercado", Importe = 200m, Porcentaje = 66m },
            new() { Categoria = "Transporte", Importe = 100m, Porcentaje = 34m }
        };

        var evolucion = new List<DashboardEvolucionMensualDto>
        {
            new() { Anio = hoy.Year, Mes = hoy.Month, Gastos = 350m, Ingresos = 1200m, Etiqueta = "Actual" },
            new() { Anio = hoy.AddMonths(-1).Year, Mes = hoy.AddMonths(-1).Month, Gastos = 180m, Ingresos = 1200m, Etiqueta = "M-1" },
            new() { Anio = hoy.AddMonths(-2).Year, Mes = hoy.AddMonths(-2).Month, Gastos = 190m, Ingresos = 1200m, Etiqueta = "M-2" },
            new() { Anio = hoy.AddMonths(-3).Year, Mes = hoy.AddMonths(-3).Month, Gastos = 170m, Ingresos = 1200m, Etiqueta = "M-3" }
        };

        var gastosMesActualDetalle = Enumerable
            .Range(1, 10)
            .Select(dia => new DashboardGastoDiaAnaliticaDto(new DateTime(hoy.Year, hoy.Month, dia), 50m))
            .ToList();

        var gastoCategoriaMensual = new List<DashboardGastoCategoriaMesAnaliticaDto>
        {
            new(hoy.Year, hoy.Month, "Supermercado", 200m),
            new(hoy.Year, hoy.Month, "Transporte", 100m),
            new(hoy.AddMonths(-1).Year, hoy.AddMonths(-1).Month, "Supermercado", 140m),
            new(hoy.AddMonths(-2).Year, hoy.AddMonths(-2).Month, "Supermercado", 150m),
            new(hoy.AddMonths(-3).Year, hoy.AddMonths(-3).Month, "Supermercado", 120m)
        };

        var alertas = servicio.GenerarAlertasProactivas(
            resumen,
            distribucion,
            evolucion,
            gastosMesActualDetalle,
            gastoCategoriaMensual,
            hoy.Month,
            hoy.Year);

        Assert.Contains(alertas, a => a.Tipo == "prediccion");
    }

    [Fact]
    public void GenerarAlertasProactivas_MesNoActual_NoGeneraPrediccion()
    {
        var servicio = new AnaliticaPredictivaService();
        var hoy = DateTime.Today;
        var periodo = hoy.AddMonths(-1);

        var alertas = servicio.GenerarAlertasProactivas(
            new DashboardResumenDto { GastosMes = 500m, IngresosMes = 1500m },
            new List<DashboardCategoriaGastoDto>
            {
                new() { Categoria = "Supermercado", Importe = 300m, Porcentaje = 60m }
            },
            new List<DashboardEvolucionMensualDto>
            {
                new() { Anio = periodo.Year, Mes = periodo.Month, Gastos = 500m, Ingresos = 1500m, Etiqueta = "Periodo" },
                new() { Anio = periodo.AddMonths(-1).Year, Mes = periodo.AddMonths(-1).Month, Gastos = 450m, Ingresos = 1500m, Etiqueta = "M-1" },
                new() { Anio = periodo.AddMonths(-2).Year, Mes = periodo.AddMonths(-2).Month, Gastos = 420m, Ingresos = 1500m, Etiqueta = "M-2" },
                new() { Anio = periodo.AddMonths(-3).Year, Mes = periodo.AddMonths(-3).Month, Gastos = 410m, Ingresos = 1500m, Etiqueta = "M-3" }
            },
            new List<DashboardGastoDiaAnaliticaDto>
            {
                new(new DateTime(periodo.Year, periodo.Month, 1), 40m),
                new(new DateTime(periodo.Year, periodo.Month, 2), 40m)
            },
            new List<DashboardGastoCategoriaMesAnaliticaDto>(),
            periodo.Month,
            periodo.Year);

        Assert.DoesNotContain(alertas, a => a.Tipo == "prediccion");
    }
}

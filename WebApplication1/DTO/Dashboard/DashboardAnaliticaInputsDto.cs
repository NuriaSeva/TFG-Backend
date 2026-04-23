namespace FinMind.DTO.Dashboard;

public sealed record DashboardGastoDiaAnaliticaDto(DateTime Fecha, decimal Importe);

public sealed record DashboardGastoCategoriaMesAnaliticaDto(int Anio, int Mes, string Categoria, decimal Importe);

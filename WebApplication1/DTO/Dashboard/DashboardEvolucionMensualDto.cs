namespace FinMind.DTO.Dashboard;

public class DashboardEvolucionMensualDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string Etiqueta { get; set; } = string.Empty;
    public decimal Gastos { get; set; }
    public decimal Ingresos { get; set; }
}
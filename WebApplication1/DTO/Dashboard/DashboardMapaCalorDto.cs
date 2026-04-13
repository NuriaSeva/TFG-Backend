namespace FinMind.DTO.Dashboard;

public class DashboardMapaCalorDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public decimal MaximoGastoDia { get; set; }
    public List<DashboardMapaCalorDiaDto> Dias { get; set; } = new();
}
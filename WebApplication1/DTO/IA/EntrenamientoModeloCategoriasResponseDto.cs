namespace FinMind.DTO.IA;

public class EntrenamientoModeloCategoriasResponseDto
{
    public bool ModeloDisponible { get; set; }
    public bool ModeloEntrenadoEnEjecucion { get; set; }
    public decimal MacroAccuracy { get; set; }
    public decimal MicroAccuracy { get; set; }
    public int RegistrosEntrenamiento { get; set; }
    public int CategoriasDetectadas { get; set; }
    public string RutaDataset { get; set; } = string.Empty;
    public string RutaModelo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public DateTime FechaModeloUtc { get; set; }
}

namespace FinMind.DTO.IA;

public class SugerenciaCategoriaResponseDto
{
    public CategoriaSugeridaDto? MejorSugerencia { get; set; }
    public List<CategoriaSugeridaDto> Alternativas { get; set; } = new();
    public decimal Confianza { get; set; }
    public string Fuente { get; set; } = "modelo-global";
    public bool RequiereConfirmacion { get; set; }
    public decimal UmbralAutoasignacion { get; set; }
}

namespace FinMind.DTO.IA;

public class CategoriaSugeridaDto
{
    public Guid? CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public decimal Confianza { get; set; }
    public string Fuente { get; set; } = "modelo-global";
}

namespace FinMind.DTO.IA;

public class SugerenciaCategoriaRequestDto
{
    public string Descripcion { get; set; } = string.Empty;
    public decimal Importe { get; set; }
    public int Tipo { get; set; }
    public Guid? UsuarioId { get; set; }
}

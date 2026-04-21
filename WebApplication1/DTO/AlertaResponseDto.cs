namespace FinMind.DTO;

public class AlertaResponseDto
{
    public Guid Id { get; set; }
    public int Tipo { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public bool Leida { get; set; }
    public DateTime FechaCreacion { get; set; }
}

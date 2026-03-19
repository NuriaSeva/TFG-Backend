namespace FindMind.DTO;

public class CuentaSeleccionadaResponseDto
{
    public Guid Id { get; set; }
    public string? IdCuentaExterna { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Banco { get; set; }
    public string? Iban { get; set; }
    public string Moneda { get; set; } = "EUR";
    public string? Tipo { get; set; }
    public decimal? SaldoActual { get; set; }
    public DateTime? FechaUltimaSincronizacion { get; set; }
}
namespace FinMind.Models.Enitdades;

public enum EstadoSincronizacion
{
    Correcta = 1,
    Error = 2,
    Parcial = 3
}

public class SincronizacionBancaria
{
    public Guid Id { get; set; }

    public Guid ConexionBancariaId { get; set; }

    public DateTime FechaInicio { get; set; }

    public DateTime? FechaFin { get; set; }

    public EstadoSincronizacion Estado { get; set; }

    public string? MensajeError { get; set; }

    public int NumeroMovimientosImportados { get; set; }

    public ConexionBancaria ConexionBancaria { get; set; } = null!;
}
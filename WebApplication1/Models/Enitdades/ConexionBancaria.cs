namespace FindMind.Models.Enitdades;

public enum ProveedorBancario
{
    TrueLayer = 1,
    GoCardless = 2,
    Tink = 3,
    Otro = 4
}

public enum EstadoConexionBancaria
{
    Pendiente = 1,
    Activa = 2,
    Expirada = 3,
    Error = 4,
    Desvinculada = 5
}

public class ConexionBancaria
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public ProveedorBancario Proveedor { get; set; }

    public EstadoConexionBancaria Estado { get; set; } = EstadoConexionBancaria.Pendiente;

    public string IdConexionExterna { get; set; } = null!;

    public string? AccessToken { get; set; }

    public string? RefreshToken { get; set; }

    public DateTime? FechaExpiracionToken { get; set; }

    public DateTime? FechaUltimaSincronizacion { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;

    public CuentaBancaria? CuentaBancaria { get; set; }

    public ICollection<SincronizacionBancaria> Sincronizaciones { get; set; } = new List<SincronizacionBancaria>();
}
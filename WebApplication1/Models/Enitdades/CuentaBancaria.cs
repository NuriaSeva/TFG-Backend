namespace FindMind.Models.Enitdades;

public class CuentaBancaria
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public Guid? ConexionBancariaId { get; set; }

    public string? IdCuentaExterna { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Iban { get; set; }

    public string? Banco { get; set; }

    public string? BIC { get; set; }

    public string Moneda { get; set; } = "EUR";

    public string? Tipo { get; set; }

    public bool Activa { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;

    public ConexionBancaria? ConexionBancaria { get; set; }

    public ICollection<Transaccion> Transacciones { get; set; } = new List<Transaccion>();
}
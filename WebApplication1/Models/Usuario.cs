namespace FindMind.Models;

public class Usuario
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Apellidos { get; set; }

    public string MonedaPreferida { get; set; } = "EUR";

    public string Idioma { get; set; } = "es";

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public DateTime? FechaUltimoAcceso { get; set; }

    public DateTime? FechaCambioPassword { get; set; }

    public ConfiguracionUsuario? ConfiguracionUsuario { get; set; }

    public CuentaBancaria? CuentaBancaria { get; set; }

    public ICollection<ConexionBancaria> ConexionesBancarias { get; set; } = new List<ConexionBancaria>();

    public ICollection<Alerta> Alertas { get; set; } = new List<Alerta>();

    public ICollection<Exportacion> Exportaciones { get; set; } = new List<Exportacion>();

    public ICollection<Categoria> Categorias { get; set; } = new List<Categoria>();

    public ICollection<Transaccion> Transacciones { get; set; } = new List<Transaccion>();
}
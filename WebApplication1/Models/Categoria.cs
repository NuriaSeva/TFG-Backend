namespace FindMind.Models;

public enum TipoCategoria
{
    Ingreso = 1,
    Gasto = 2
}

public class Categoria
{
    public Guid Id { get; set; }

    public Guid? UsuarioId { get; set; }

    public string Nombre { get; set; } = null!;

    public TipoCategoria Tipo { get; set; }

    public string? Color { get; set; }

    public string? Icono { get; set; }

    public bool EsSistema { get; set; } = false;

    public bool Archivada { get; set; } = false;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Usuario? Usuario { get; set; }

    public ICollection<Transaccion> Transacciones { get; set; } = new List<Transaccion>();
}
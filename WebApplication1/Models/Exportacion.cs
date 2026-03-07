namespace FindMind.Models;

public enum EstadoExportacion
{
    Pendiente = 1,
    Generada = 2,
    Error = 3
}

public class Exportacion
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public FormatoExportacion Formato { get; set; }

    public EstadoExportacion Estado { get; set; } = EstadoExportacion.Pendiente;

    public string? RutaArchivo { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;
}
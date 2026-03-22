namespace FinMind.Models.Enitdades;

public enum TipoAlerta
{
    GastoInusual = 1,
    PresupuestoSuperado = 2,
    Prediccion = 3,
    ErrorSincronizacion = 4,
    Informativa = 5
}

public class Alerta
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public TipoAlerta Tipo { get; set; }

    public string Titulo { get; set; } = null!;

    public string Mensaje { get; set; } = null!;

    public bool Leida { get; set; } = false;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;
}
namespace FinMind.Models.Enitdades;

public enum TamanoTexto
{
    Pequeno = 1,
    Mediano = 2,
    Grande = 3
}

public enum FormatoExportacion
{
    Csv = 1,
    Pdf = 2,
    Excel = 3
}

public enum TemaAplicacion
{
    Claro = 1,
    Oscuro = 2,
    Sistema = 3
}

public class ConfiguracionUsuario
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public TamanoTexto TamanoTexto { get; set; } = TamanoTexto.Mediano;

    public bool NotificacionesActivas { get; set; } = true;

    public FormatoExportacion FormatoExportacionPreferido { get; set; } = FormatoExportacion.Csv;

    public TemaAplicacion Tema { get; set; } = TemaAplicacion.Sistema;

    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;
}
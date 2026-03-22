namespace FinMind.Models.Enitdades;

public enum TipoTransaccion
{
    Ingreso = 1,
    Gasto = 2
}

public enum OrigenTransaccion
{
    Manual = 1,
    Banco = 2
}

public enum ProveedorTransaccion
{
    Ninguno = 0,
    Tink = 1
}

public class Transaccion
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public Guid? CuentaBancariaId { get; set; }

    public Guid? CategoriaId { get; set; }

    public decimal Importe { get; set; }

    public string Moneda { get; set; } = "EUR";

    public TipoTransaccion Tipo { get; set; }

    public OrigenTransaccion Origen { get; set; } = OrigenTransaccion.Manual;

    public ProveedorTransaccion Proveedor { get; set; } = ProveedorTransaccion.Ninguno;

    public DateTime Fecha { get; set; }

    public string? Descripcion { get; set; }

    public string? IdTransaccionExterna { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;

    public CuentaBancaria? CuentaBancaria { get; set; }

    public Categoria? Categoria { get; set; }
}
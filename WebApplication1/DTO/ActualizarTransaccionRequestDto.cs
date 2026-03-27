public class ActualizarTransaccionRequestDto
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid? CuentaBancariaId { get; set; }
    public Guid? CategoriaId { get; set; }
    public decimal Importe { get; set; }
    public string Moneda { get; set; } = "EUR";
    public int Tipo { get; set; }
    public int Origen { get; set; }
    public int Proveedor { get; set; }
    public DateTime Fecha { get; set; }
    public string? Descripcion { get; set; }
    public string? IdTransaccionExterna { get; set; }
}
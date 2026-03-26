namespace FinMind.DTO
{
    public class CrearTransaccionManualRequestDto
    {
        public Guid UsuarioId { get; set; }
        public Guid? CuentaBancariaId { get; set; }
        public Guid? CategoriaId { get; set; }
        public decimal Importe { get; set; }
        public int Tipo { get; set; } // 1 Ingreso, 2 Gasto
        public DateTime Fecha { get; set; }
        public string? Descripcion { get; set; }
        public string? Moneda { get; set; } = "EUR";
    }
}
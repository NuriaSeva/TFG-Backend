using System.ComponentModel.DataAnnotations;

namespace FinMind.DTO
{
    public class CrearTransaccionManualRequestDto
    {
        public Guid? CuentaBancariaId { get; set; }
        public Guid? CategoriaId { get; set; }

        [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
        public decimal Importe { get; set; }

        [Range(1, 2)] // 1 Ingreso, 2 Gasto
        public int Tipo { get; set; }

        public DateTime Fecha { get; set; }

        [StringLength(500)]
        public string? Descripcion { get; set; }

        [StringLength(3, MinimumLength = 3)]
        public string? Moneda { get; set; } = "EUR";
    }

    public sealed class ActualizarCategoriaTransaccionRequest
    {
        public Guid? CategoriaId { get; set; }
    }
}

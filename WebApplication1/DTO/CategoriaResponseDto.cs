namespace FinMind.DTO
{
    public class CategoriaResponseDto
    {
        public Guid Id { get; set; }
        public Guid? UsuarioId { get; set; }
        public string Nombre { get; set; } = null!;
        public int Tipo { get; set; }
        public string? Color { get; set; }
        public string? Icono { get; set; }
        public bool EsSistema { get; set; }
        public bool Archivada { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}

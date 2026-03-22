namespace FinMind.DTO
{
    public class ResultadoSincronizacionTransaccionesDto
    {
        public int TotalRecibidas { get; set; }
        public int Nuevas { get; set; }
        public int Ignoradas { get; set; }
    }
}
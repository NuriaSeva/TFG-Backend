using FinMind.DTO;

namespace FinMind.Interfaces
{
    public interface ITransaccionesService
    {
        Task<ResultadoSincronizacionTransaccionesDto> SincronizarDesdeTinkAsync(Guid usuarioId);
    }
}

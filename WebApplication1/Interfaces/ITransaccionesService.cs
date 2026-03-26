using FinMind.DTO;
using FinMind.Models.Enitdades;

namespace FinMind.Interfaces
{
    public interface ITransaccionesService
    {
        Task<ResultadoSincronizacionTransaccionesDto> SincronizarDesdeTinkAsync(Guid usuarioId);

        Task<PaginacionDTO<TransaccionesUsuarioResponseDto>> ObtenerPorUsuarioAsync(
            Guid usuarioId,
            int? mes = null,
            int? anio = null,
            int? tipo = null,
            string? texto = null,
            int pagina = 1,
            int tamanyoPagina = 20);

        Task<TransaccionesUsuarioResponseDto> CrearManualAsync(CrearTransaccionManualRequestDto request);
    } }
    

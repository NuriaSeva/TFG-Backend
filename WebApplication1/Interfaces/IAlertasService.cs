using FinMind.DTO;

namespace FinMind.Interfaces;

public interface IAlertasService
{
    Task<PaginacionDTO<AlertaResponseDto>> ObtenerAsync(Guid usuarioId, int pagina = 1, int tamanyo = 20);
    Task<int> ObtenerNoLeidasTotalAsync(Guid usuarioId);
    Task<bool> MarcarLeidaAsync(Guid id, Guid usuarioId);
    Task MarcarTodasLeidasAsync(Guid usuarioId);
}

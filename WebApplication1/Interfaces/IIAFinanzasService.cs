using FinMind.DTO.IA;

namespace FinMind.Interfaces;

public interface IIAFinanzasService
{
    Task<EntrenamientoModeloCategoriasResponseDto> EntrenarModeloCategoriasAsync(bool forzar = false, CancellationToken cancellationToken = default);

    Task<SugerenciaCategoriaResponseDto> SugerirCategoriaAsync(SugerenciaCategoriaRequestDto request, CancellationToken cancellationToken = default);
}

using FinMind.DTO;
using FinMind.Models.Enitdades;

namespace FinMind.Interfaces;

public interface ICategoriasService
{
    Task<int> ImportarCategoriasTinkAsync(string locale = "es_ES");
    Task<CategoriaResponseDto?> ObtenerPorIdAsync(Guid id);
    Task<List<CategoriaResponseDto>> ObtenerPorUsuarioAsync(Guid usuarioId);
    Task<CategoriaResponseDto> CrearAsync(Categoria categoria, Guid usuarioId);
    Task ActualizarAsync(Guid id, Categoria categoria);
    Task EliminarAsync(Guid id, Guid usuarioId);
    Task<int> ObtenerImpactoEliminacionAsync(Guid id, Guid usuarioId);
}

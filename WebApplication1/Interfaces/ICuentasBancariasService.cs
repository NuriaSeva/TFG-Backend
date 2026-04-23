using FinMind.DTO;
using FinMind.Models.Enitdades;

namespace FinMind.Interfaces;

public interface ICuentasBancariasService
{
    Task<CuentaSeleccionadaResponseDto?> ObtenerCuentaPorUsuarioAsync(Guid usuarioId);
    Task ActualizarCuentaAsync(Guid id, CuentaBancaria cuenta);
    Task EliminarCuentaAsync(Guid id);
}

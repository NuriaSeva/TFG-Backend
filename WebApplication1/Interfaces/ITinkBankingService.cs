using FinMind.DTO;
using FinMind.DTO.Banking;

namespace FinMind.Interfaces;

public interface ITinkBankingService
{
    Task<object> GetLoginUrlAsync(string localUserId);
    Task<AccountCheckCallbackResultDto> HandleAccountCheckCallbackAsync(
        string localUserId,
        IDictionary<string, string> queryParams);
    Task<AccountCheckCallbackResultDto?> GetLastAccountCheckResultAsync(string localUserId);
    Task<string> GetClientAccessTokenAsync();
    Task<string> GetAccountVerificationReportRawAsync(string reportId);
    Task<CuentaSeleccionadaResponseDto> GuardarCuentaDesdeAccountCheckAsync(Guid usuarioId, string reportId);
    Task<CuentaSeleccionadaResponseDto> ProcesarCallbackYGuardarCuentaAsync(
        string localUserId,
        IDictionary<string, string> queryParams);

    // NUEVO: flujo separado para transactions
    Task<object> GetTransactionsLoginUrlAsync(string localUserId);
    Task GuardarTokensTransactionsAsync(Guid usuarioId, string code);
    Task<string> ObtenerAccessTokenVigenteAsync(Guid usuarioId);
    Task<string> GetTransactionsRawAsync(Guid usuarioId, string? cuentaExternaId, Guid idCuenta);
}
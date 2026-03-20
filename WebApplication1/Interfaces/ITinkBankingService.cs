using FindMind.DTO;
using FindMind.DTO.Banking;

namespace FindMind.Interfaces;

public interface ITinkBankingService
{
    Task<object> GetLoginUrlAsync(string localUserId);

    Task<AccountCheckCallbackResultDto> HandleAccountCheckCallbackAsync(
        string localUserId,
        IDictionary<string, string> queryParams);

    Task<AccountCheckCallbackResultDto?> GetLastAccountCheckResultAsync(string localUserId);

    Task<string> GetClientAccessTokenAsync();
    Task<string> GetAccountVerificationReportRawAsync(string reportId);

    Task<string> GetAccountsRawAsync(string localUserId);
    Task<string> GetTransactionsRawAsync(string localUserId);

    Task<object> ExchangeCodeAsync(string localUserId, string code);
    Task<CuentaSeleccionadaResponseDto> GuardarCuentaDesdeAccountCheckAsync(Guid usuarioId, string reportId);
    Task<CuentaSeleccionadaResponseDto> ProcesarCallbackYGuardarCuentaAsync(
    string localUserId,
    IDictionary<string, string> queryParams);
}
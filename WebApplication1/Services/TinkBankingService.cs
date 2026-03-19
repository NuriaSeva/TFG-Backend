using FindMind.Data;
using FindMind.DTO;
using FindMind.DTO.Banking;
using FindMind.Interfaces;
using FindMind.Models.Enitdades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FindMind.Services;

public class TinkBankingService : ITinkBankingService
{
    private readonly HttpClient _httpClient;
    private readonly FindMindDbContext _context;
    private readonly TinkOptions _options;

    // Compatibilidad temporal con el flujo antiguo OAuth
    private static readonly Dictionary<string, string> _tokens = new();

    // Guardamos el último callback de Account Check por usuario local
    private static readonly Dictionary<string, AccountCheckCallbackResultDto> _accountCheckResults = new();

    public TinkBankingService(
        HttpClient httpClient,
        FindMindDbContext context,
        IOptions<TinkOptions> options)
    {
        _httpClient = httpClient;
        _context = context;
        _options = options.Value;
    }

    public Task<object> GetLoginUrlAsync(string localUserId)
    {
        var state = Guid.NewGuid().ToString("N");
        var separator = _options.LinkUrl.Contains('?') ? "&" : "?";

        var callbackUri =
            $"{_options.RedirectUri}?localUserId={Uri.EscapeDataString(localUserId)}";

        var loginUrl =
            $"{_options.LinkUrl}" +
            $"{separator}redirect_uri={Uri.EscapeDataString(callbackUri)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&auto_redirect_mobile=true";

        return Task.FromResult<object>(new
        {
            localUserId,
            state,
            loginUrl
        });
    }

    public Task<AccountCheckCallbackResultDto> HandleAccountCheckCallbackAsync(
        string localUserId,
        IDictionary<string, string> queryParams)
    {
        var state = queryParams.TryGetValue("state", out var stateValue)
            ? stateValue
            : null;

        var error = queryParams.TryGetValue("error", out var errorValue)
            ? errorValue
            : null;

        var errorDescription = queryParams.TryGetValue("error_description", out var errorDescriptionValue)
            ? errorDescriptionValue
            : null;

        var reportId = queryParams.TryGetValue("account_verification_report_id", out var reportIdValue)
            ? reportIdValue
            : null;

        var result = new AccountCheckCallbackResultDto
        {
            LocalUserId = localUserId,
            State = state,
            Success = string.IsNullOrWhiteSpace(error),
            Error = error,
            ErrorDescription = errorDescription,
            AccountVerificationReportId = reportId,
            Received = new Dictionary<string, string>(queryParams)
        };

        _accountCheckResults[localUserId] = result;

        return Task.FromResult(result);
    }

    public Task<AccountCheckCallbackResultDto?> GetLastAccountCheckResultAsync(string localUserId)
    {
        _accountCheckResults.TryGetValue(localUserId, out var result);
        return Task.FromResult(result);
    }

    public async Task<string> GetClientAccessTokenAsync()
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "client_credentials",
            ["scope"] = "account-verification-reports:read"
        };

        using var response = await _httpClient.PostAsync(
            $"{_options.BaseUrl}/api/v1/oauth/token",
            new FormUrlEncodedContent(form));

        var rawJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Error al obtener client access token. Status: {(int)response.StatusCode}. Body: {rawJson}");
        }

        using var doc = JsonDocument.Parse(rawJson);

        var accessToken = doc.RootElement.GetProperty("access_token").GetString();

        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Tink no devolvió client access token.");

        return accessToken;
    }

    public async Task<string> GetAccountVerificationReportRawAsync(string reportId)
    {
        if (string.IsNullOrWhiteSpace(reportId))
            throw new ArgumentException("El reportId es obligatorio.", nameof(reportId));

        var clientAccessToken = await GetClientAccessTokenAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_options.BaseUrl}/api/v1/account-verification-reports/{Uri.EscapeDataString(reportId)}");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientAccessToken);

        using var response = await _httpClient.SendAsync(request);
        var rawJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Error al obtener account verification report. Status: {(int)response.StatusCode}. Body: {rawJson}");
        }

        return rawJson;
    }

    public async Task<CuentaSeleccionadaResponseDto> GuardarCuentaDesdeAccountCheckAsync(
        Guid usuarioId,
        string reportId)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El usuarioId es obligatorio.", nameof(usuarioId));

        if (string.IsNullOrWhiteSpace(reportId))
            throw new ArgumentException("El reportId es obligatorio.", nameof(reportId));

        var rawJson = await GetAccountVerificationReportRawAsync(reportId);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var report = JsonSerializer.Deserialize<TinkAccountVerificationReportDto>(rawJson, options);

        if (report?.UserDataByProvider == null || report.UserDataByProvider.Count == 0)
            throw new InvalidOperationException("El reporte de Account Check no contiene proveedores.");

        var provider = report.UserDataByProvider
            .FirstOrDefault(p => p.Accounts != null && p.Accounts.Count > 0);

        if (provider == null)
            throw new InvalidOperationException("El reporte de Account Check no contiene cuentas.");

        // La cuenta ya viene elegida desde Tink
        var account = provider.Accounts.First();

        var iban = account.Iban
            ?? account.AccountIdentifiers?.Iban?.Iban
            ?? account.AccountNumber;

        var bic = account.AccountIdentifiers?.Iban?.Bic;

        var conexion = await _context.ConexionesBancarias
            .FirstOrDefaultAsync(c =>
                c.UsuarioId == usuarioId &&
                c.Proveedor == ProveedorBancario.Tink &&
                c.Estado != EstadoConexionBancaria.Desvinculada);

        if (conexion == null)
        {
            conexion = new ConexionBancaria
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                Proveedor = ProveedorBancario.Tink,
                Estado = EstadoConexionBancaria.Activa,
                IdConexionExterna = reportId,
                AccessToken = null,
                RefreshToken = null,
                FechaExpiracionToken = null,
                FechaUltimaSincronizacion = DateTime.UtcNow,
                FechaCreacion = DateTime.UtcNow
            };

            _context.ConexionesBancarias.Add(conexion);
        }
        else
        {
            conexion.Estado = EstadoConexionBancaria.Activa;
            conexion.IdConexionExterna = reportId;
            conexion.FechaUltimaSincronizacion = DateTime.UtcNow;
        }

        var cuentaExistente = await _context.CuentasBancarias
            .FirstOrDefaultAsync(c =>
                c.UsuarioId == usuarioId &&
                c.IdCuentaExterna == account.Id);

        if (cuentaExistente == null)
        {
            cuentaExistente = new CuentaBancaria
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                ConexionBancariaId = conexion.Id,
                IdCuentaExterna = account.Id,
                Nombre = !string.IsNullOrWhiteSpace(account.Name)
                    ? account.Name!
                    : "Cuenta principal",
                Iban = iban,
                BIC = bic,
                Banco = provider.FinancialInstitutionName,
                Moneda = account.CurrencyCode ?? "EUR",
                Tipo = account.AccountType,
                SaldoActual = null,
                FechaUltimaSincronizacion = DateTime.UtcNow,
                Activa = true,
                FechaCreacion = DateTime.UtcNow
            };

            _context.CuentasBancarias.Add(cuentaExistente);
        }
        else
        {
            cuentaExistente.ConexionBancariaId = conexion.Id;
            cuentaExistente.Nombre = !string.IsNullOrWhiteSpace(account.Name)
                ? account.Name!
                : cuentaExistente.Nombre;
            cuentaExistente.Iban = iban;
            cuentaExistente.BIC = bic;
            cuentaExistente.Banco = provider.FinancialInstitutionName;
            cuentaExistente.Moneda = account.CurrencyCode ?? cuentaExistente.Moneda;
            cuentaExistente.Tipo = account.AccountType;
            cuentaExistente.FechaUltimaSincronizacion = DateTime.UtcNow;
            cuentaExistente.Activa = true;
        }

        await _context.SaveChangesAsync();

        return new CuentaSeleccionadaResponseDto
        {
            Id = cuentaExistente.Id,
            IdCuentaExterna = cuentaExistente.IdCuentaExterna,
            Nombre = cuentaExistente.Nombre,
            Banco = cuentaExistente.Banco,
            Iban = cuentaExistente.Iban,
            Moneda = cuentaExistente.Moneda,
            Tipo = cuentaExistente.Tipo,
            FechaUltimaSincronizacion= cuentaExistente.FechaUltimaSincronizacion,
            SaldoActual = cuentaExistente.SaldoActual
        };
    }

    // =========================
    // Flujo antiguo OAuth
    // =========================

    public async Task<object> ExchangeCodeAsync(string localUserId, string code)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code
        };

        using var response = await _httpClient.PostAsync(
            $"{_options.BaseUrl}/api/v1/oauth/token",
            new FormUrlEncodedContent(form));

        var rawJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Error al intercambiar code por token. Status: {(int)response.StatusCode}. Body: {rawJson}");
        }

        using var doc = JsonDocument.Parse(rawJson);

        var accessToken = doc.RootElement.GetProperty("access_token").GetString();

        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Tink no devolvió access_token.");

        _tokens[localUserId] = accessToken;

        return new
        {
            localUserId,
            accessToken
        };
    }

    public async Task<string> GetAccountsRawAsync(string localUserId)
    {
        if (!_tokens.TryGetValue(localUserId, out var token))
            throw new InvalidOperationException("Primero debes autenticar y guardar el token.");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_options.BaseUrl}/data/v2/accounts");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request);
        var rawJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Error al obtener accounts. Status: {(int)response.StatusCode}. Body: {rawJson}");
        }

        return rawJson;
    }

    public async Task<string> GetTransactionsRawAsync(string localUserId)
    {
        if (!_tokens.TryGetValue(localUserId, out var token))
            throw new InvalidOperationException("Primero debes autenticar y guardar el token.");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_options.BaseUrl}/data/v2/transactions");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request);
        var rawJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Error al obtener transactions. Status: {(int)response.StatusCode}. Body: {rawJson}");
        }

        return rawJson;
    }
    }

   
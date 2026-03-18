using FindMind.Interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

public class TinkBankingService : ITinkBankingService
{
    private readonly HttpClient _httpClient;
    private readonly TinkOptions _options;

    // Solo para pruebas
    private static readonly Dictionary<string, string> _tokens = new();

    public TinkBankingService(HttpClient httpClient, IOptions<TinkOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<object> GetLoginUrlAsync(string localUserId)
    {
        return Task.FromResult<object>(new
        {
            localUserId,
            loginUrl = _options.TransactionsLinkUrl
        });
    }

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
            accessToken = accessToken
        };
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
    public async Task<string> GetAccountsRawAsync(string localUserId)
    {
        if (!_tokens.TryGetValue(localUserId, out var token))
            throw new InvalidOperationException("Primero debes autenticar y guardar el token.");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_options.BaseUrl}/data/v2/accounts");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request);
        var rawJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Error al obtener accounts. Status: {(int)response.StatusCode}. Body: {rawJson}");
        }

        return rawJson;
    }
}
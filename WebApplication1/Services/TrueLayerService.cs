using FindMind.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FindMind.Services;

public class TrueLayerService
{
    private readonly HttpClient _httpClient;
    private readonly TrueLayerOptions _options;

    public TrueLayerService(HttpClient httpClient, IOptions<TrueLayerOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<TokenResponse?> IntercambiarCodePorTokenAsync(string code, CancellationToken cancellationToken = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["code"] = code
        };

        using var content = new FormUrlEncodedContent(form);

        using var response = await _httpClient.PostAsync(
            "https://auth.truelayer-sandbox.com/connect/token",
            content,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Error obteniendo token de autenticación: {body}");

        return JsonSerializer.Deserialize<TokenResponse>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public async Task<string> ObtenerCuentasRawAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.truelayer-sandbox.com/data/v1/accounts");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Error obteniendo cuentas del usuario: {body}");

        return body;
    }

    public async Task<string> ObtenerTransaccionesRawAsync(string accountId, string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.truelayer-sandbox.com/data/v1/accounts/{accountId}/transactions");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Error obteniendo transacciones de la cuenta bancaria por: {body}");

        return body;
    }
}
public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = null!;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
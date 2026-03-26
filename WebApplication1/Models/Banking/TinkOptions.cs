public class TinkOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.tink.com";
    public string LinkUrlTransacciones { get; set; } = string.Empty;
    public string LinkUrlCuentas { get; set; } = string.Empty;
    public object RedirectUriTransactions { get; set; } = string.Empty;
}
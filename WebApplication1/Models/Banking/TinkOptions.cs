public class TinkOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string RedirectUriTransactions { get; set; } = string.Empty;
    public string Market { get; set; } = "ES";
    public string Locale { get; set; } = "es_ES";
    public string InputProvider { get; set; } = "es-demobank-password";
}
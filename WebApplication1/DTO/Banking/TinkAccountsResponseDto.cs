using System.Text.Json.Serialization;

namespace FinMind.DTO.Banking;

public class TinkAccountsResponseDto
{
    [JsonPropertyName("accounts")]
    public List<TinkDataAccountDto>? Accounts { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

public class TinkDataAccountDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("identifiers")]
    public TinkAccountIdentifiersDto? Identifiers { get; set; }
}

public class TinkAccountIdentifiersDto
{
    [JsonPropertyName("iban")]
    public TinkIbanIdentifierDto? Iban { get; set; }

    [JsonPropertyName("financialInstitution")]
    public TinkFinancialInstitutionIdentifierDto? FinancialInstitution { get; set; }
}

public class TinkIbanIdentifierDto
{
    [JsonPropertyName("iban")]
    public string? Iban { get; set; }

    [JsonPropertyName("bban")]
    public string? Bban { get; set; }
}

public class TinkFinancialInstitutionIdentifierDto
{
    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }
}
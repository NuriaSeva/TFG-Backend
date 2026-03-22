using System.Text.Json.Serialization;

namespace FinMind.DTO.Banking;

public class TinkTransactionsResponseDto
{
    [JsonPropertyName("transactions")]
    public List<TinkTransactionDto>? Transactions { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

public class TinkTransactionDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("accountId")]
    public string? AccountId { get; set; }

    [JsonPropertyName("amount")]
    public TinkMoneyAmountDto? Amount { get; set; }

    [JsonPropertyName("descriptions")]
    public TinkTransactionDescriptionsDto? Descriptions { get; set; }

    [JsonPropertyName("dates")]
    public TinkTransactionDatesDto? Dates { get; set; }

    [JsonPropertyName("identifiers")]
    public TinkTransactionIdentifiersDto? Identifiers { get; set; }

    [JsonPropertyName("types")]
    public TinkTransactionTypesDto? Types { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("providerMutability")]
    public string? ProviderMutability { get; set; }
}

public class TinkTransactionDescriptionsDto
{
    [JsonPropertyName("original")]
    public string? Original { get; set; }

    [JsonPropertyName("display")]
    public string? Display { get; set; }
}

public class TinkTransactionDatesDto
{
    [JsonPropertyName("booked")]
    public string? Booked { get; set; }
}

public class TinkTransactionIdentifiersDto
{
    [JsonPropertyName("providerTransactionId")]
    public string? ProviderTransactionId { get; set; }
}

public class TinkTransactionTypesDto
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class TinkMoneyAmountDto
{
    [JsonPropertyName("value")]
    public TinkDecimalValueDto? Value { get; set; }

    [JsonPropertyName("currencyCode")]
    public string? CurrencyCode { get; set; }
}

public class TinkDecimalValueDto
{
    [JsonPropertyName("unscaledValue")]
    public string? UnscaledValue { get; set; }

    [JsonPropertyName("scale")]
    public string? Scale { get; set; }
}
namespace FindMind.DTO.Banking;

public class TinkAccountsResponseDto
{
    public List<TinkAccountDto> Accounts { get; set; } = new();
}

public class TinkAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Type { get; set; }
    public string? FinancialInstitutionName { get; set; }
    public string? BankAccountType { get; set; }
    public string? AccountCategory { get; set; }

    public TinkAccountNumberDto? AccountNumber { get; set; }

    public List<TinkBalanceDto> Balances { get; set; } = new();
}

public class TinkAccountNumberDto
{
    public string? Iban { get; set; }
    public string? Bic { get; set; }
    public string? Number { get; set; }
    public string? SortCode { get; set; }
}

public class TinkBalanceDto
{
    public decimal? Balance { get; set; }
    public string? CurrencyCode { get; set; }
    public string? ReferenceDate { get; set; }
}
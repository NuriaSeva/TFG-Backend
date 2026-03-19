namespace FindMind.DTO.Banking;

public class TinkAccountVerificationReportDto
{
    public string Id { get; set; } = string.Empty;
    public long Created { get; set; }
    public List<TinkReportProviderDto> UserDataByProvider { get; set; } = new();
}

public class TinkReportProviderDto
{
    public string ProviderName { get; set; } = string.Empty;
    public long Updated { get; set; }
    public string FinancialInstitutionName { get; set; } = string.Empty;
    public TinkReportIdentityDto? Identity { get; set; }
    public List<TinkReportAccountDto> Accounts { get; set; } = new();
}

public class TinkReportIdentityDto
{
    public string? Name { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Ssn { get; set; }
}

public class TinkReportAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public string? DisplayAccountNumber { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Name { get; set; }
    public string? Iban { get; set; }
    public string? HolderName { get; set; }
    public TinkReportAccountIdentifiersDto? AccountIdentifiers { get; set; }
    public List<TinkReportPartyDto> Parties { get; set; } = new();
    public string? AccountType { get; set; }
    public string? CustomerSegment { get; set; }
}

public class TinkReportAccountIdentifiersDto
{
    public TinkReportIbanDto? Iban { get; set; }
}

public class TinkReportIbanDto
{
    public string? Iban { get; set; }
    public string? Bic { get; set; }
    public string? Bban { get; set; }
}

public class TinkReportPartyDto
{
    public string? Role { get; set; }
    public TinkReportIdentityDto? Identity { get; set; }
}
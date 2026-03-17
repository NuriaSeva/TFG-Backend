namespace FindMind.DTO.Banking
{
 
    public class TinkAccountsResponseDto
    {
        public List<TinkAccountDto> Accounts { get; set; } = new();
    }

    public class TinkAccountDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public string FinancialInstitutionName { get; set; } = string.Empty;
        public List<TinkBalanceDto> Balances { get; set; } = new();
    }

    public class TinkBalanceDto
    {
        public decimal Balance { get; set; }
    }
}

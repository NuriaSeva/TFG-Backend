namespace FindMind.DTO.Banking
{
    public class TinkTransactionsResponseDto
    {
        public List<TinkTransactionDto> Transactions { get; set; } = new();
    }

    public class TinkTransactionDto
    {
        public string Id { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
    }
}

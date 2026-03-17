namespace FindMind.Interfaces

{
    public interface ITinkBankingService
    {
        Task<object> GetLoginUrlAsync(string localUserId);
        Task<object> ExchangeCodeAsync(string localUserId, string code);
        Task<string> GetTransactionsRawAsync(string localUserId);

        Task<string> GetAccountsRawAsync(string localUserId);
    }
}

namespace WalletLedger.Api.Application.Interfaces
{
    public interface IWalletService
    {
        Task<Guid> CreateWalletAsync(Guid userId, string currency);
        Task<decimal> GetBalanceAsync(Guid walletId);
    }
}

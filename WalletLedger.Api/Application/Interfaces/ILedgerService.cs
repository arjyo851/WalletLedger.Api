namespace WalletLedger.Api.Application.Interfaces
{
    public interface ILedgerService
    {
        Task CreditAsync(Guid walletId, decimal amount, string referenceId);
        Task DebitAsync(Guid walletId, decimal amount, string referenceId);
    }

}

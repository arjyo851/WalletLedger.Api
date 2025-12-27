using WalletLedger.Api.Contracts.Responses;

namespace WalletLedger.Api.Application.Interfaces
{
    public interface ILedgerService
    {
        Task CreditAsync(Guid walletId, decimal amount, string referenceId);
        Task DebitAsync(Guid walletId, decimal amount, string referenceId);
        Task<TransactionHistoryResponse> GetTransactionHistoryAsync(
            Guid walletId,
            int pageNumber,
            int pageSize,
            DateTime? startDate,
            DateTime? endDate,
            Domain.Entities.LedgerEntryType? type,
            decimal? minAmount,
            decimal? maxAmount,
            string sortBy,
            string sortOrder);
        Task<TransactionResponse?> GetTransactionByIdAsync(Guid transactionId);
        Task<TransactionResponse?> GetTransactionByReferenceIdAsync(Guid walletId, string referenceId);
        Task<TransferResponse> TransferBetweenWalletsAsync(
            Guid fromWalletId,
            Guid toWalletId,
            decimal amount,
            string referenceId);
    }

}

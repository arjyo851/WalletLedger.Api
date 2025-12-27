using WalletLedger.Api.Contracts.Responses;

namespace WalletLedger.Api.Application.Interfaces
{
    public interface IWalletService
    {
        Task<Guid> CreateWalletAsync(Guid userId, string currency);
        Task<decimal> GetBalanceAsync(Guid walletId);
        Task ValidateWalletOwnership(Guid walletId, Guid userId);
        Task<WalletListResponse> GetUserWalletsAsync(Guid userId, string? currency = null);
        Task<WalletDetailResponse?> GetWalletDetailsAsync(Guid walletId);
        Task UpdateWalletStatusAsync(Guid walletId, Guid userId, Domain.Entities.WalletStatus status);
        Task ValidateWalletStatusForTransaction(Guid walletId);
        Task<decimal> GetBalanceAtPointInTimeAsync(Guid walletId, DateTime asOfDate);
        Task CreateBalanceSnapshotAsync(Guid walletId);
        Task<BalanceHistoryResponse> GetBalanceHistoryAsync(
            Guid walletId,
            DateTime? startDate,
            DateTime? endDate,
            int pageNumber,
            int pageSize);
    }
}

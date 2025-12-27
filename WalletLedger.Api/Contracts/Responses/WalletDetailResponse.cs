using WalletLedger.Api.Domain.Entities;

namespace WalletLedger.Api.Contracts.Responses;

public record WalletDetailResponse(
    Guid Id,
    Guid UserId,
    string Currency,
    decimal Balance,
    WalletStatus Status,
    DateTime CreatedAt,
    DateTime? LastTransactionDate
);


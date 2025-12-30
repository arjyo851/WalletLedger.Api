namespace WalletLedger.Api.Contracts.Responses;

public record BalanceSnapshotResponse(
    Guid Id,
    Guid WalletId,
    decimal Balance,
    DateTime SnapshotDate
);







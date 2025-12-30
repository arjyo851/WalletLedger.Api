namespace WalletLedger.Api.Contracts.Responses;

public record BalanceHistoryResponse(
    List<BalanceSnapshotResponse> Snapshots,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages
);







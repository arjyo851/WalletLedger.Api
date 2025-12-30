namespace WalletLedger.Api.Contracts.Responses;

public record TransactionHistoryResponse(
    List<TransactionResponse> Transactions,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages
);







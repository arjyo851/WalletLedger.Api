namespace WalletLedger.Api.Contracts.Responses;

public record TransferResponse(
    Guid FromTransactionId,
    Guid ToTransactionId,
    Guid FromWalletId,
    Guid ToWalletId,
    decimal Amount,
    string ReferenceId
);



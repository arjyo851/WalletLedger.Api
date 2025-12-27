namespace WalletLedger.Api.Contracts.Requests;

public record TransferRequest(
    Guid FromWalletId,
    Guid ToWalletId,
    decimal Amount,
    string ReferenceId
);



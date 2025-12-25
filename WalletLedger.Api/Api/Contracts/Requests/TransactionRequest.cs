namespace WalletLedger.Api.Contracts.Requests
{
    public record TransactionRequest(
    
        Guid WalletId,
        decimal Amount,
        string ReferenceId
    );
}

namespace WalletLedger.Api.Contracts.Requests
{
    public record CreateWalletRequest
    (
        Guid UserId,
        string Currency
    );
}

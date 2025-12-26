namespace WalletLedger.Api.Contracts.Responses
{
    public record WalletResponse(
        Guid WalletId,
        string Currency
    );

}

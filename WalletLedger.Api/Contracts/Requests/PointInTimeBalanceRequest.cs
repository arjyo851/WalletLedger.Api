namespace WalletLedger.Api.Contracts.Requests;

public record PointInTimeBalanceRequest
{
    public Guid WalletId { get; init; }
    public DateTime AsOfDate { get; init; }
}



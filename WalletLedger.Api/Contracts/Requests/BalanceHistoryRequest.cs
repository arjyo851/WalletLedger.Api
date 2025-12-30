namespace WalletLedger.Api.Contracts.Requests;

public record BalanceHistoryRequest
{
    public Guid WalletId { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}







using WalletLedger.Api.Domain.Entities;

namespace WalletLedger.Api.Contracts.Requests;

public record TransactionHistoryRequest
{
    public Guid WalletId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public LedgerEntryType? Type { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public string? SortBy { get; init; } = "CreatedAt";
    public string? SortOrder { get; init; } = "desc";
}


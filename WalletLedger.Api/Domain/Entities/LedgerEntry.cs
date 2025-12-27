namespace WalletLedger.Api.Domain.Entities;

public class LedgerEntry
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public decimal Amount { get; set; }
    public LedgerEntryType Type { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public TransactionStatus Status { get; set; } = TransactionStatus.Completed;
    public DateTime CreatedAt { get; set; }
}


namespace WalletLedger.Api.Domain.Entities;

public class BalanceSnapshot
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public decimal Balance { get; set; }
    public DateTime SnapshotDate { get; set; }
    public DateTime CreatedAt { get; set; }
}




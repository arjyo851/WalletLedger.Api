namespace WalletLedger.Api.Domain.Entities;

public class Wallet
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}


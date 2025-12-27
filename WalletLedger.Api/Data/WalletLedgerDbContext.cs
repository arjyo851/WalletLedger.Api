using Microsoft.EntityFrameworkCore;
using WalletLedger.Api.Domain.Entities;

namespace WalletLedger.Api.Data;

public class WalletLedgerDbContext : DbContext
{
    public WalletLedgerDbContext(DbContextOptions<WalletLedgerDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Wallet> Wallets { get; set; }
    public DbSet<LedgerEntry> LedgerEntries { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.Email)
                  .IsRequired()
                  .HasMaxLength(255);

        });

        // Wallet configuration
        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.ToTable("Wallets");
            entity.HasKey(e => e.Id);
            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.Currency)
                  .IsRequired()
                  .HasMaxLength(3);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(e => new { e.UserId, e.Currency }).IsUnique();
        });

        // LedgerEntry configuration
        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.ToTable("LedgerEntries", t => t.HasCheckConstraint("CK_LedgerEntries_Amount_Positive", "[Amount]>0"));
            entity.HasKey(e => e.Id);
            entity.HasOne<Wallet>()
                  .WithMany()
                  .HasForeignKey(e => e.WalletId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.ReferenceId)
                  .IsRequired()
                  .HasMaxLength(100);
            entity.HasIndex(e => new { e.WalletId, e.ReferenceId }).IsUnique();
            entity.HasIndex(e => new { e.WalletId, e.CreatedAt });
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        });

        // RefreshToken configuration
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash)
                  .IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });
    }
}


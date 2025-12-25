using Microsoft.EntityFrameworkCore;
using WalletLedger.Api.Application.Interfaces;
using WalletLedger.Api.Data;
using WalletLedger.Api.Domain.Entities;

namespace WalletLedger.Api.Application.Services
{
    public class LedgerService : ILedgerService
    {
        private readonly WalletLedgerDbContext _db;

        public LedgerService(WalletLedgerDbContext db)
        {
            _db = db;
        }

        public async Task CreditAsync(Guid walletId, decimal amount, string referenceId)
        {
            await ExecuteLedgerOperation(
                walletId,
                amount,
                referenceId,
                LedgerEntryType.Credit
            );
        }

        public async Task DebitAsync(Guid walletId, decimal amount, string referenceId)
        {
            await ExecuteLedgerOperation(
                walletId,
                amount,
                referenceId,
                LedgerEntryType.Debit
            );
        }

        private async Task ExecuteLedgerOperation(
        Guid walletId,
        decimal amount,
        string referenceId,
        LedgerEntryType type)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive");

            using var transaction =
                await _db.Database.BeginTransactionAsync();

            var walletExists = await _db.Wallets
                .AnyAsync(w => w.Id == walletId);

            if (!walletExists)
                throw new InvalidOperationException("Wallet does not exist");

            var alreadyProcessed = await _db.LedgerEntries
                .AnyAsync(l =>
                    l.WalletId == walletId &&
                    l.ReferenceId == referenceId);

            if (alreadyProcessed)
            {
                await transaction.RollbackAsync();
                return; 
            }

            if (type == LedgerEntryType.Debit)
            {
                var balance = await GetBalanceInternal(walletId);

                if (balance < amount)
                    throw new InvalidOperationException("Insufficient balance");
            }

            var entry = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                WalletId = walletId,
                Amount = amount,
                Type = type,
                ReferenceId = referenceId,
                CreatedAt = DateTime.UtcNow
            };

            _db.LedgerEntries.Add(entry);
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
        }

        private async Task<decimal> GetBalanceInternal(Guid walletId)
        {
            var credits = await _db.LedgerEntries
                .Where(l => l.WalletId == walletId &&
                            l.Type == LedgerEntryType.Credit)
                .SumAsync(l => (decimal?)l.Amount) ?? 0;

            var debits = await _db.LedgerEntries
                .Where(l => l.WalletId == walletId &&
                            l.Type == LedgerEntryType.Debit)
                .SumAsync(l => (decimal?)l.Amount) ?? 0;

            return credits - debits;
        }
    }
}

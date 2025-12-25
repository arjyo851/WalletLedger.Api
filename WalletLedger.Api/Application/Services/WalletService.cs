using Microsoft.EntityFrameworkCore;
using WalletLedger.Api.Application.Interfaces;
using WalletLedger.Api.Data;
using WalletLedger.Api.Domain.Entities;

namespace WalletLedger.Api.Application.Services
{
    public class WalletService : IWalletService
    {
        private readonly WalletLedgerDbContext _db;

        public WalletService(WalletLedgerDbContext db)
        {
            _db = db;
        }

        public async Task<Guid> CreateWalletAsync(Guid userId, string currency)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == userId);
            if(!userExists)
            {
                throw new InvalidOperationException("User does not exist.");
            }

            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Currency = currency.ToUpperInvariant(),
                CreatedAt = DateTime.UtcNow
            };

            _db.Wallets.Add(wallet);
            await _db.SaveChangesAsync();

            return wallet.Id;
        }

        public async Task<decimal> GetBalanceAsync(Guid walletId)
        {
            var wallet = await _db.Wallets.AnyAsync(w => w.Id == walletId);
            if(!wallet)
            {
                throw new InvalidOperationException("Wallet does not exist.");
            }

            var credits = await _db.LedgerEntries
                .Where(l => l.WalletId == walletId && l.Type == LedgerEntryType.Credit)
                .SumAsync(l => (decimal?)l.Amount) ?? 0m;

            var debits = await _db.LedgerEntries
                .Where(l => l.WalletId == walletId && l.Type == LedgerEntryType.Debit)
                .SumAsync(l => (decimal?)l.Amount) ?? 0m;

            return credits - debits;
        }
    }

}

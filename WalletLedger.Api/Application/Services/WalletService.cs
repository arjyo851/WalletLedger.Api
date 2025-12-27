using Microsoft.EntityFrameworkCore;
using WalletLedger.Api.Application.Interfaces;
using WalletLedger.Api.Contracts.Responses;
using WalletLedger.Api.Data;
using WalletLedger.Api.Domain.Entities;

namespace WalletLedger.Api.Application.Services
{
    public class WalletService : IWalletService
    {
        private readonly WalletLedgerDbContext _db;
        private readonly ICacheService _cache;
        private const string BalanceCacheKeyPrefix = "balance:";
        private const string WalletCacheKeyPrefix = "wallet:";

        public WalletService(WalletLedgerDbContext db, ICacheService cache)
        {
            _db = db;
            _cache = cache;
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
            var cacheKey = $"{BalanceCacheKeyPrefix}{walletId}";
            
            // Try to get from cache
            var cachedBalance = await _cache.GetAsync<decimal?>(cacheKey);
            if (cachedBalance.HasValue)
            {
                return cachedBalance.Value;
            }

            var wallet = await _db.Wallets.AnyAsync(w => w.Id == walletId);
            if(!wallet)
            {
                throw new InvalidOperationException("Wallet does not exist.");
            }

            // Only count completed transactions (exclude failed)
            var credits = await _db.LedgerEntries
                .Where(l => l.WalletId == walletId && 
                           l.Type == LedgerEntryType.Credit &&
                           l.Status == TransactionStatus.Completed)
                .SumAsync(l => (decimal?)l.Amount) ?? 0m;

            var debits = await _db.LedgerEntries
                .Where(l => l.WalletId == walletId && 
                           l.Type == LedgerEntryType.Debit &&
                           l.Status == TransactionStatus.Completed)
                .SumAsync(l => (decimal?)l.Amount) ?? 0m;

            var balance = credits - debits;

            // Cache the balance for 1 minute
            await _cache.SetAsync(cacheKey, balance, TimeSpan.FromMinutes(1));

            return balance;
        }

        private async Task InvalidateBalanceCacheAsync(Guid walletId)
        {
            var cacheKey = $"{BalanceCacheKeyPrefix}{walletId}";
            await _cache.RemoveAsync(cacheKey);
        }

        public async Task ValidateWalletOwnership(Guid walletId, Guid userId)
        {
            var ownsWallet = await _db.Wallets
                .AnyAsync(w => w.Id == walletId && w.UserId == userId);

            if (!ownsWallet)
                throw new InvalidOperationException("Unauthorized wallet access");
        }

        public async Task<WalletListResponse> GetUserWalletsAsync(Guid userId, string? currency = null)
        {
            var query = _db.Wallets
                .Where(w => w.UserId == userId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(currency))
            {
                query = query.Where(w => w.Currency == currency.ToUpperInvariant());
            }

            var wallets = await query.ToListAsync();

            var walletDetails = new List<WalletDetailResponse>();

            foreach (var wallet in wallets)
            {
                var balance = await GetBalanceAsync(wallet.Id);

                var lastTransactionDate = await _db.LedgerEntries
                    .Where(l => l.WalletId == wallet.Id)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => (DateTime?)l.CreatedAt)
                    .FirstOrDefaultAsync();

                walletDetails.Add(new WalletDetailResponse(
                    wallet.Id,
                    wallet.UserId,
                    wallet.Currency,
                    balance,
                    wallet.Status,
                    wallet.CreatedAt,
                    lastTransactionDate
                ));
            }

            return new WalletListResponse(walletDetails, walletDetails.Count);
        }

        public async Task<WalletDetailResponse?> GetWalletDetailsAsync(Guid walletId)
        {
            var wallet = await _db.Wallets
                .FirstOrDefaultAsync(w => w.Id == walletId);

            if (wallet == null)
                return null;

            var balance = await GetBalanceAsync(walletId);

            var lastTransactionDate = await _db.LedgerEntries
                .Where(l => l.WalletId == walletId)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => (DateTime?)l.CreatedAt)
                .FirstOrDefaultAsync();

            return new WalletDetailResponse(
                wallet.Id,
                wallet.UserId,
                wallet.Currency,
                balance,
                wallet.Status,
                wallet.CreatedAt,
                lastTransactionDate
            );
        }

        public async Task UpdateWalletStatusAsync(Guid walletId, Guid userId, WalletStatus status)
        {
            await ValidateWalletOwnership(walletId, userId);

            var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.Id == walletId);
            if (wallet == null)
                throw new InvalidOperationException("Wallet does not exist.");

            wallet.Status = status;
            await _db.SaveChangesAsync();
        }

        public async Task ValidateWalletStatusForTransaction(Guid walletId)
        {
            var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.Id == walletId);
            if (wallet == null)
                throw new InvalidOperationException("Wallet does not exist.");

            if (wallet.Status == WalletStatus.Suspended || wallet.Status == WalletStatus.Frozen)
            {
                throw new InvalidOperationException($"Wallet is {wallet.Status} and cannot process transactions.");
            }

            if (wallet.Status == WalletStatus.Closed)
            {
                throw new InvalidOperationException("Wallet is closed and cannot process transactions.");
            }
        }

        public async Task<decimal> GetBalanceAtPointInTimeAsync(Guid walletId, DateTime asOfDate)
        {
            var wallet = await _db.Wallets.AnyAsync(w => w.Id == walletId);
            if (!wallet)
            {
                throw new InvalidOperationException("Wallet does not exist.");
            }

            // Calculate balance up to the specified date
            var credits = await _db.LedgerEntries
                .Where(l => l.WalletId == walletId &&
                           l.Type == LedgerEntryType.Credit &&
                           l.Status == TransactionStatus.Completed &&
                           l.CreatedAt <= asOfDate)
                .SumAsync(l => (decimal?)l.Amount) ?? 0m;

            var debits = await _db.LedgerEntries
                .Where(l => l.WalletId == walletId &&
                           l.Type == LedgerEntryType.Debit &&
                           l.Status == TransactionStatus.Completed &&
                           l.CreatedAt <= asOfDate)
                .SumAsync(l => (decimal?)l.Amount) ?? 0m;

            return credits - debits;
        }

        public async Task CreateBalanceSnapshotAsync(Guid walletId)
        {
            var wallet = await _db.Wallets.AnyAsync(w => w.Id == walletId);
            if (!wallet)
            {
                throw new InvalidOperationException("Wallet does not exist.");
            }

            var balance = await GetBalanceAsync(walletId);

            var snapshot = new BalanceSnapshot
            {
                Id = Guid.NewGuid(),
                WalletId = walletId,
                Balance = balance,
                SnapshotDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _db.BalanceSnapshots.Add(snapshot);
            await _db.SaveChangesAsync();
        }

        public async Task<BalanceHistoryResponse> GetBalanceHistoryAsync(
            Guid walletId,
            DateTime? startDate,
            DateTime? endDate,
            int pageNumber,
            int pageSize)
        {
            var wallet = await _db.Wallets.AnyAsync(w => w.Id == walletId);
            if (!wallet)
            {
                throw new InvalidOperationException("Wallet does not exist.");
            }

            var query = _db.BalanceSnapshots
                .Where(s => s.WalletId == walletId)
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(s => s.SnapshotDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(s => s.SnapshotDate <= endDate.Value);
            }

            var totalCount = await query.CountAsync();

            var snapshots = await query
                .OrderByDescending(s => s.SnapshotDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new BalanceSnapshotResponse(
                    s.Id,
                    s.WalletId,
                    s.Balance,
                    s.SnapshotDate
                ))
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return new BalanceHistoryResponse(
                snapshots,
                totalCount,
                pageNumber,
                pageSize,
                totalPages
            );
        }

    }

}

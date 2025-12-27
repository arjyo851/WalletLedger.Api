using Microsoft.EntityFrameworkCore;
using WalletLedger.Api.Application.Interfaces;
using WalletLedger.Api.Contracts.Responses;
using WalletLedger.Api.Data;
using WalletLedger.Api.Domain.Entities;

namespace WalletLedger.Api.Application.Services
{
    public class LedgerService : ILedgerService
    {
        private readonly WalletLedgerDbContext _db;
        private readonly ICacheService _cache;
        private const string BalanceCacheKeyPrefix = "balance:";

        public LedgerService(WalletLedgerDbContext db, ICacheService cache)
        {
            _db = db;
            _cache = cache;
        }

        private async Task InvalidateBalanceCacheAsync(Guid walletId)
        {
            var cacheKey = $"{BalanceCacheKeyPrefix}{walletId}";
            await _cache.RemoveAsync(cacheKey);
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

            // Rate limiting check for debit operations: Prevents abuse by limiting debits to 5 per wallet per minute
            if (type == LedgerEntryType.Debit)
            {
                // Count debit entries for this wallet created in the last 1 minute
                var recentDebitCount = await _db.LedgerEntries
                    .Where(l =>
                        l.WalletId == walletId &&
                        l.Type == LedgerEntryType.Debit &&
                        l.CreatedAt > DateTime.UtcNow.AddMinutes(-1))
                    .CountAsync();

                // If 5 or more debits occurred in the last minute, reject the request
                if (recentDebitCount >= 5)
                {
                    throw new InvalidOperationException(
                        "Too many debit attempts. Please try later."
                    );
                }
            }

            using var transaction =
                await _db.Database.BeginTransactionAsync();

            var wallet = await _db.Wallets
                .FirstOrDefaultAsync(w => w.Id == walletId);

            if (wallet == null)
                throw new InvalidOperationException("Wallet does not exist");

            if (wallet.Status == WalletStatus.Suspended || wallet.Status == WalletStatus.Frozen)
            {
                throw new InvalidOperationException($"Wallet is {wallet.Status} and cannot process transactions.");
            }

            if (wallet.Status == WalletStatus.Closed)
            {
                throw new InvalidOperationException("Wallet is closed and cannot process transactions.");
            }

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
                Status = TransactionStatus.Completed,
                CreatedAt = DateTime.UtcNow
            };

            _db.LedgerEntries.Add(entry);
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            // Invalidate balance cache
            await InvalidateBalanceCacheAsync(walletId);
        }

        private async Task<decimal> GetBalanceInternal(Guid walletId)
        {
            // Only count completed transactions (exclude failed)
            var credits = await _db.LedgerEntries
                .Where(l => l.WalletId == walletId &&
                            l.Type == LedgerEntryType.Credit &&
                            l.Status == TransactionStatus.Completed)
                .SumAsync(l => (decimal?)l.Amount) ?? 0;

            var debits = await _db.LedgerEntries
                .Where(l => l.WalletId == walletId &&
                            l.Type == LedgerEntryType.Debit &&
                            l.Status == TransactionStatus.Completed)
                .SumAsync(l => (decimal?)l.Amount) ?? 0;

            return credits - debits;
        }

        public async Task<TransactionHistoryResponse> GetTransactionHistoryAsync(
            Guid walletId,
            int pageNumber,
            int pageSize,
            DateTime? startDate,
            DateTime? endDate,
            LedgerEntryType? type,
            decimal? minAmount,
            decimal? maxAmount,
            string sortBy,
            string sortOrder)
        {
            var query = _db.LedgerEntries
                .Where(l => l.WalletId == walletId)
                .AsQueryable();

            // Apply filters
            if (startDate.HasValue)
            {
                query = query.Where(l => l.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(l => l.CreatedAt <= endDate.Value);
            }

            if (type.HasValue)
            {
                query = query.Where(l => l.Type == type.Value);
            }

            if (minAmount.HasValue)
            {
                query = query.Where(l => l.Amount >= minAmount.Value);
            }

            if (maxAmount.HasValue)
            {
                query = query.Where(l => l.Amount <= maxAmount.Value);
            }

            // Apply sorting
            query = sortBy.ToLower() switch
            {
                "amount" => sortOrder.ToLower() == "asc"
                    ? query.OrderBy(l => l.Amount)
                    : query.OrderByDescending(l => l.Amount),
                "type" => sortOrder.ToLower() == "asc"
                    ? query.OrderBy(l => l.Type)
                    : query.OrderByDescending(l => l.Type),
                "createdat" or _ => sortOrder.ToLower() == "asc"
                    ? query.OrderBy(l => l.CreatedAt)
                    : query.OrderByDescending(l => l.CreatedAt)
            };

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply pagination
            var transactions = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new TransactionResponse(
                    l.Id,
                    l.WalletId,
                    l.Amount,
                    l.Type,
                    l.ReferenceId,
                    l.Status,
                    l.CreatedAt
                ))
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return new TransactionHistoryResponse(
                transactions,
                totalCount,
                pageNumber,
                pageSize,
                totalPages
            );
        }

        public async Task<TransactionResponse?> GetTransactionByIdAsync(Guid transactionId)
        {
            var entry = await _db.LedgerEntries
                .FirstOrDefaultAsync(l => l.Id == transactionId);

            if (entry == null)
                return null;

            return new TransactionResponse(
                entry.Id,
                entry.WalletId,
                entry.Amount,
                entry.Type,
                entry.ReferenceId,
                entry.Status,
                entry.CreatedAt
            );
        }

        public async Task<TransactionResponse?> GetTransactionByReferenceIdAsync(Guid walletId, string referenceId)
        {
            var entry = await _db.LedgerEntries
                .FirstOrDefaultAsync(l => l.WalletId == walletId && l.ReferenceId == referenceId);

            if (entry == null)
                return null;

            return new TransactionResponse(
                entry.Id,
                entry.WalletId,
                entry.Amount,
                entry.Type,
                entry.ReferenceId,
                entry.Status,
                entry.CreatedAt
            );
        }

        public async Task<TransferResponse> TransferBetweenWalletsAsync(
            Guid fromWalletId,
            Guid toWalletId,
            decimal amount,
            string referenceId)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive");

            if (fromWalletId == toWalletId)
                throw new ArgumentException("Source and destination wallets cannot be the same");

            using var transaction = await _db.Database.BeginTransactionAsync();

            // Validate both wallets exist and are active
            var fromWallet = await _db.Wallets.FirstOrDefaultAsync(w => w.Id == fromWalletId);
            var toWallet = await _db.Wallets.FirstOrDefaultAsync(w => w.Id == toWalletId);

            if (fromWallet == null)
                throw new InvalidOperationException("Source wallet does not exist");

            if (toWallet == null)
                throw new InvalidOperationException("Destination wallet does not exist");

            // Enforce same currency only
            if (fromWallet.Currency != toWallet.Currency)
                throw new InvalidOperationException("Transfers are only allowed between wallets with the same currency");

            if (fromWallet.Status == WalletStatus.Suspended || fromWallet.Status == WalletStatus.Frozen || fromWallet.Status == WalletStatus.Closed)
                throw new InvalidOperationException($"Source wallet is {fromWallet.Status} and cannot process transactions.");

            if (toWallet.Status == WalletStatus.Suspended || toWallet.Status == WalletStatus.Frozen || toWallet.Status == WalletStatus.Closed)
                throw new InvalidOperationException($"Destination wallet is {toWallet.Status} and cannot process transactions.");

            // Check if reference ID already processed
            var alreadyProcessed = await _db.LedgerEntries
                .AnyAsync(l => l.WalletId == fromWalletId && l.ReferenceId == referenceId);

            if (alreadyProcessed)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException("Transfer with this reference ID has already been processed");
            }

            // Check balance
            var balance = await GetBalanceInternal(fromWalletId);

            if (balance < amount)
                throw new InvalidOperationException("Insufficient balance for transfer");

            // Create debit entry
            var debitEntry = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                WalletId = fromWalletId,
                Amount = amount,
                Type = LedgerEntryType.Debit,
                ReferenceId = referenceId,
                Status = TransactionStatus.Completed,
                CreatedAt = DateTime.UtcNow
            };

            // Create credit entry
            var creditEntry = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                WalletId = toWalletId,
                Amount = amount,
                Type = LedgerEntryType.Credit,
                ReferenceId = referenceId,
                Status = TransactionStatus.Completed,
                CreatedAt = DateTime.UtcNow
            };

            _db.LedgerEntries.Add(debitEntry);
            _db.LedgerEntries.Add(creditEntry);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            // Invalidate balance cache for both wallets
            await InvalidateBalanceCacheAsync(fromWalletId);
            await InvalidateBalanceCacheAsync(toWalletId);

            return new TransferResponse(
                debitEntry.Id,
                creditEntry.Id,
                fromWalletId,
                toWalletId,
                amount,
                referenceId
            );
        }
    }
}

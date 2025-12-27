using WalletLedger.Api.Application.Interfaces;
using WalletLedger.Api.Application.Services;
using WalletLedger.Api.Contracts.Responses;
using WalletLedger.Api.Data;
using WalletLedger.Api.Domain.Entities;
using Xunit;

namespace WalletLedger.Tests.TestHelpers
{
    // Simple in-memory cache implementation for testing
    public class TestCacheService : ICacheService
    {
        private readonly Dictionary<string, string> _cache = new();

        public Task<T?> GetAsync<T>(string key)
        {
            if (_cache.TryGetValue(key, out var value))
            {
                return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(value));
            }
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            var serialized = System.Text.Json.JsonSerializer.Serialize(value);
            _cache[key] = serialized;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }

        public Task RemoveByPatternAsync(string pattern)
        {
            var keysToRemove = _cache.Keys.Where(k => k.Contains(pattern)).ToList();
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
            return Task.CompletedTask;
        }
    }

    public class LedgerIntegrationTests : IAsyncLifetime
    {
        private WalletLedgerDbContext _db = null!;
        private WalletService _walletService = null!;
        private LedgerService _ledgerService = null!;
        private ICacheService _cacheService = null!;

        public async Task InitializeAsync()
        {
            _db = DbContextFactory.Create();
            _cacheService = new TestCacheService();
            _walletService = new WalletService(_db, _cacheService);
            _ledgerService = new LedgerService(_db, _cacheService);
        }

        public async Task DisposeAsync()
        {
            await _db.DisposeAsync();
        }

        [Fact]
        public async Task Wallet_Ledger_Full_Flow_Works_Correctly()
        {
            // Arrange - Create user
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "Test User",
                Email = "test@test.com",
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Create wallet
            var walletId = await _walletService.CreateWalletAsync(user.Id, "INR");

            // Act
            await _ledgerService.CreditAsync(walletId, 100, "REF-A");
            await _ledgerService.DebitAsync(walletId, 40, "REF-B");

            var balance = await _walletService.GetBalanceAsync(walletId);

            // Assert
            Assert.Equal(60, balance);

            // Retry same debit (idempotency)
            await _ledgerService.DebitAsync(walletId, 40, "REF-B");

            var balanceAfterRetry = await _walletService.GetBalanceAsync(walletId);
            Assert.Equal(60, balanceAfterRetry);

            // Insufficient balance
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _ledgerService.DebitAsync(walletId, 100, "REF-C")
            );
        }
    }
}

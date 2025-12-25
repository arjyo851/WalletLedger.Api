using WalletLedger.Api.Application.Services;
using WalletLedger.Api.Data;
using WalletLedger.Api.Domain.Entities;
using Xunit;

namespace WalletLedger.Tests.TestHelpers
{
    public class LedgerIntegrationTests : IAsyncLifetime
    {
        private WalletLedgerDbContext _db = null!;
        private WalletService _walletService = null!;
        private LedgerService _ledgerService = null!;

        public async Task InitializeAsync()
        {
            _db = DbContextFactory.Create();
            _walletService = new WalletService(_db);
            _ledgerService = new LedgerService(_db);
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

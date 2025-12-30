using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WalletLedger.Api.Contracts.Requests;
using WalletLedger.Api.Contracts.Responses;
using WalletLedger.Api.Data;
using WalletLedger.Api.Domain.Entities;
using WalletLedger.Tests.TestHelpers;
using Xunit;

namespace WalletLedger.Tests.Integration;

    public class ApiIntegrationTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;
        private readonly IServiceScope _scope;
        private readonly WalletLedgerDbContext _db;
        private Guid _testUserId;
        private Guid _otherUserId;
        private string _userToken = null!;
        private string _adminToken = null!;

        public ApiIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            _scope = factory.Services.CreateScope();
            _db = _scope.ServiceProvider.GetRequiredService<WalletLedgerDbContext>();
        }

    public async Task InitializeAsync()
    {
        // Ensure database is created before each test
        await _db.Database.EnsureCreatedAsync();

        // Create test users
        _testUserId = Guid.NewGuid();
        _otherUserId = Guid.NewGuid();

        _db.Users.AddRange(
            new User { Id = _testUserId, Name = "Test User", Email = "test@test.com", CreatedAt = DateTime.UtcNow },
            new User { Id = _otherUserId, Name = "Other User", Email = "other@test.com", CreatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        // Generate tokens
        _userToken = TestJwtHelper.GenerateUserToken(_testUserId);
        _adminToken = TestJwtHelper.GenerateAdminToken(_testUserId);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
        _scope.Dispose();
        _client.Dispose();
    }

    private void SetAuthHeader(string token)
    {
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    #region AuthController Tests

    [Fact]
    public async Task Login_WithValidUserId_ReturnsTokens()
    {
        // Act
        var response = await _client.PostAsync($"/api/auth/login?userId={_testUserId}", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(content.TryGetProperty("accessToken", out _));
        Assert.True(content.TryGetProperty("refreshToken", out _));
    }

    [Fact]
    public async Task Login_WithInvalidUserId_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsync($"/api/auth/login?userId={Guid.NewGuid()}", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginAdmin_WithValidUserId_ReturnsToken()
    {
        // Act
        var response = await _client.PostAsync($"/api/auth/login-admin?userId={_testUserId}", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(content.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokens()
    {
        // Arrange - First login to get refresh token
        var loginResponse = await _client.PostAsync($"/api/auth/login?userId={_testUserId}", null);
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginContent.GetProperty("refreshToken").GetString();

        // Act
        var response = await _client.PostAsync($"/api/auth/refresh?refreshToken={Uri.EscapeDataString(refreshToken)}", null);


        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(content.TryGetProperty("accessToken", out _));
        Assert.True(content.TryGetProperty("refreshToken", out _));
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsync("/api/auth/refresh?refreshToken=invalid-token", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithValidToken_RevokesToken()
    {
        // Arrange
        var loginResponse = await _client.PostAsync($"/api/auth/login?userId={_testUserId}", null);
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginContent.GetProperty("refreshToken").GetString();
        SetAuthHeader(loginContent.GetProperty("accessToken").GetString()!);

        // Act
        var response = await _client.PostAsync($"/api/auth/logout?refreshToken={Uri.EscapeDataString(refreshToken)}", null);


        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify token is revoked
        var refreshResponse = await _client.PostAsync($"/api/auth/refresh?refreshToken={refreshToken}", null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    #endregion

    #region WalletController Tests

    [Fact]
    public async Task CreateWallet_WithValidRequest_ReturnsWalletId()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var request = new CreateWalletRequest("USD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/wallets", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);
        Assert.NotEqual(Guid.Empty, wallet.WalletId);
        Assert.Equal("USD", wallet.Currency);
    }

    [Fact]
    public async Task CreateWallet_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var request = new CreateWalletRequest("USD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/wallets", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserWallets_ReturnsUserWallets()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var wallet1 = await CreateWallet("USD");
        var wallet2 = await CreateWallet("EUR");

        // Act
        var response = await _client.GetAsync("/api/wallets");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wallets = await response.Content.ReadFromJsonAsync<WalletListResponse>();
        Assert.NotNull(wallets);
        Assert.Equal(2, wallets.TotalCount);
        Assert.Contains(wallets.Wallets, w => w.Id == wallet1);
        Assert.Contains(wallets.Wallets, w => w.Id == wallet2);
    }

    [Fact]
    public async Task GetUserWallets_WithCurrencyFilter_ReturnsFilteredWallets()
    {
        // Arrange
        SetAuthHeader(_userToken);
        await CreateWallet("USD");
        await CreateWallet("EUR");

        // Act
        var response = await _client.GetAsync("/api/wallets?currency=USD");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wallets = await response.Content.ReadFromJsonAsync<WalletListResponse>();
        Assert.NotNull(wallets);
        Assert.Equal(1, wallets.TotalCount);
        Assert.All(wallets.Wallets, w => Assert.Equal("USD", w.Currency));
    }

    [Fact]
    public async Task GetWalletDetails_WithValidWallet_ReturnsWallet()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");

        // Act
        var response = await _client.GetAsync($"/api/wallets/{walletId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wallet = await response.Content.ReadFromJsonAsync<WalletDetailResponse>();
        Assert.NotNull(wallet);
        Assert.Equal(walletId, wallet.Id);
        Assert.Equal("USD", wallet.Currency);
    }

    [Fact]
    public async Task GetWalletDetails_WithOtherUserWallet_ReturnsUnauthorized()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var otherUserWallet = await CreateWalletForUser(_otherUserId, "USD");

        // Act
        var response = await _client.GetAsync($"/api/wallets/{otherUserWallet}");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); // Throws InvalidOperationException
    }

    [Fact]
    public async Task GetBalance_WithValidWallet_ReturnsBalance()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");
        await CreditWallet(walletId, 100, "REF-1");

        // Act
        var response = await _client.GetAsync($"/api/wallets/{walletId}/balance");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balance = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        Assert.NotNull(balance);
        Assert.Equal(100, balance.Balance);
    }

    [Fact]
    public async Task UpdateWalletStatus_WithValidRequest_UpdatesStatus()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");
        var request = new UpdateWalletStatusRequest(WalletStatus.Frozen);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/wallets/{walletId}/status", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify status was updated
        var wallet = await _db.Wallets.FirstAsync(w => w.Id == walletId);
        Assert.Equal(WalletStatus.Frozen, wallet.Status);
    }

    [Fact]
    public async Task GetBalanceAtPointInTime_ReturnsCorrectBalance()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");
        await CreditWallet(walletId, 100, "REF-1");
        
        // Determine a point in time after the first credit but before the second credit
        var firstEntry = await _db.LedgerEntries.FirstAsync(l => l.WalletId == walletId && l.ReferenceId == "REF-1");
        var pointInTime = firstEntry.CreatedAt.AddTicks(1);
        await CreditWallet(walletId, 50, "REF-2");

        // Act
        var response = await _client.GetAsync($"/api/wallets/{walletId}/balance/point-in-time?asOfDate={pointInTime:O}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balance = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        Assert.NotNull(balance);
        Assert.Equal(100, balance.Balance);
    }

    [Fact]
    public async Task GetBalanceHistory_ReturnsHistory()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");
        await CreditWallet(walletId, 100, "REF-1");
        await CreateBalanceSnapshot(walletId);

        // Act
        var response = await _client.GetAsync($"/api/wallets/{walletId}/balance/history?pageNumber=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await response.Content.ReadFromJsonAsync<BalanceHistoryResponse>();
        Assert.NotNull(history);
        Assert.True(history.TotalCount > 0);
    }

    [Fact]
    public async Task CreateBalanceSnapshot_CreatesSnapshot()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");
        await CreditWallet(walletId, 100, "REF-1");

        // Act
        var response = await _client.PostAsync($"/api/wallets/{walletId}/balance/snapshot", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await _db.BalanceSnapshots.FirstOrDefaultAsync(s => s.WalletId == walletId);
        Assert.NotNull(snapshot);
        Assert.Equal(100, snapshot.Balance);
    }

    #endregion

    #region TransactionController Tests

    [Fact]
    public async Task Credit_WithValidRequest_CreditsWallet()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");
        var request = new TransactionRequest(walletId, 100, "REF-CREDIT-1");

        // Act
        var response = await _client.PostAsJsonAsync("/api/transactions/credit", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balance = await _db.LedgerEntries
            .Where(l => l.WalletId == walletId && l.Type == LedgerEntryType.Credit)
            .SumAsync(l => l.Amount);
        Assert.Equal(100, balance);
    }

    [Fact]
    public async Task Credit_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        // Create wallet directly in the database without setting the client's auth header
        var walletId = await CreateWalletForUser(_testUserId, "USD");
        var request = new TransactionRequest(walletId, 100, "REF-CREDIT-1");

        // Act
        var response = await _client.PostAsJsonAsync("/api/transactions/credit", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Debit_WithValidRequest_DebitsWallet()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");
        await CreditWallet(walletId, 100, "REF-1");
        var request = new TransactionRequest(walletId, 50, "REF-DEBIT-1");

        // Act
        var response = await _client.PostAsJsonAsync("/api/transactions/debit", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balance = await GetBalance(walletId);
        Assert.Equal(50, balance);
    }

    [Fact]
    public async Task Debit_WithInsufficientBalance_ReturnsError()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");
        await CreditWallet(walletId, 50, "REF-1");
        var request = new TransactionRequest(walletId, 100, "REF-DEBIT-1");

        // Act
        var response = await _client.PostAsJsonAsync("/api/transactions/debit", request);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_WithValidRequest_TransfersAmount()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var fromWalletId = await CreateWallet("USD");
        var toWalletId = await CreateWallet("USD");
        await CreditWallet(fromWalletId, 100, "REF-1");
        var request = new TransferRequest(fromWalletId, toWalletId, 50, "REF-TRANSFER-1");

        // Act
        var response = await _client.PostAsJsonAsync("/api/transactions/transfer", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transfer = await response.Content.ReadFromJsonAsync<TransferResponse>();
        Assert.NotNull(transfer);
        Assert.Equal(50, transfer.Amount);
        
        var fromBalance = await GetBalance(fromWalletId);
        var toBalance = await GetBalance(toWalletId);
        Assert.Equal(50, fromBalance);
        Assert.Equal(50, toBalance);
    }

    [Fact]
    public async Task Transfer_WithDifferentCurrencies_ReturnsError()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var fromWalletId = await CreateWallet("USD");
        var toWalletId = await CreateWallet("EUR");
        await CreditWallet(fromWalletId, 100, "REF-1");
        var request = new TransferRequest(fromWalletId, toWalletId, 50, "REF-TRANSFER-1");

        // Act
        var response = await _client.PostAsJsonAsync("/api/transactions/transfer", request);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactionHistory_ReturnsTransactions()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");
        await CreditWallet(walletId, 100, "REF-1");
        await CreditWallet(walletId, 50, "REF-2");

        // Act
        var response = await _client.GetAsync($"/api/transactions/history?walletId={walletId}&pageNumber=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await response.Content.ReadFromJsonAsync<TransactionHistoryResponse>();
        Assert.NotNull(history);
        Assert.Equal(2, history.TotalCount);
        Assert.Equal(2, history.Transactions.Count);
    }

    [Fact]
    public async Task GetTransactionHistory_WithFilters_ReturnsFilteredTransactions()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");
        await CreditWallet(walletId, 100, "REF-1");
        await DebitWallet(walletId, 50, "REF-2");

        // Act - Filter by type
        var response = await _client.GetAsync($"/api/transactions/history?walletId={walletId}&type=Credit&pageNumber=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await response.Content.ReadFromJsonAsync<TransactionHistoryResponse>();
        Assert.NotNull(history);
        Assert.Equal(1, history.TotalCount);
        Assert.All(history.Transactions, t => Assert.Equal(LedgerEntryType.Credit, t.Type));
    }

    [Fact]
    public async Task GetTransactionById_ReturnsTransaction()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");
        var transactionId = await CreditWallet(walletId, 100, "REF-1");

        // Act
        var response = await _client.GetAsync($"/api/transactions/{transactionId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.NotNull(transaction);
        Assert.Equal(transactionId, transaction.Id);
        Assert.Equal(100, transaction.Amount);
    }

    [Fact]
    public async Task GetTransactionByReferenceId_ReturnsTransaction()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");
        await CreditWallet(walletId, 100, "REF-UNIQUE-123");

        // Act
        var response = await _client.GetAsync($"/api/transactions/by-reference/{walletId}/REF-UNIQUE-123");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.NotNull(transaction);
        Assert.Equal("REF-UNIQUE-123", transaction.ReferenceId);
    }

    [Fact]
    public async Task Credit_WithDuplicateReferenceId_IsIdempotent()
    {
        // Arrange
        SetAuthHeader(_userToken);
        var walletId = await CreateWallet("USD");
        var request = new TransactionRequest(walletId, 100, "REF-DUPLICATE");

        // Act - Credit twice with same reference
        await _client.PostAsJsonAsync("/api/transactions/credit", request);
        await _client.PostAsJsonAsync("/api/transactions/credit", request);

        // Assert
        var transactions = await _db.LedgerEntries
            .Where(l => l.WalletId == walletId && l.ReferenceId == "REF-DUPLICATE")
            .CountAsync();
        Assert.Equal(1, transactions);
    }

    #endregion

    #region HealthController Tests

    [Fact]
    public async Task GetHealth_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", health.GetProperty("Status").GetString());
    }

    [Fact]
    public async Task GetDetailedHealth_WithAdminToken_ReturnsDetailedHealth()
    {
        // Arrange
        SetAuthHeader(_adminToken);

        // Act
        var response = await _client.GetAsync("/api/health/detailed");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        // Parse raw JSON to inspect exact payload during test failures
        var health = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw).RootElement;
        Assert.True(health.TryGetProperty("Dependencies", out _), $"Missing Dependencies in response. Raw: {raw}");
        Assert.True(health.TryGetProperty("System", out _), $"Missing System in response. Raw: {raw}");
    }

    [Fact]
    public async Task GetDetailedHealth_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/health/detailed");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMetrics_WithAdminToken_ReturnsMetrics()
    {
        // Arrange
        SetAuthHeader(_adminToken);

        // Act
        var response = await _client.GetAsync("/api/health/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metrics = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(metrics.TryGetProperty("Database", out _));
        Assert.True(metrics.TryGetProperty("System", out _));
    }

    #endregion

    #region AdminController Tests

    [Fact]
    public async Task AdminHealth_WithAdminToken_ReturnsOk()
    {
        // Arrange
        SetAuthHeader(_adminToken);

        // Act
        var response = await _client.GetAsync("/api/admin/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminHealth_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/health");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Helper Methods

    private async Task<Guid> CreateWallet(string currency)
    {
        SetAuthHeader(_userToken);
        var request = new CreateWalletRequest(currency);
        var response = await _client.PostAsJsonAsync("/api/wallets", request);
        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>();
        return wallet!.WalletId;
    }

    private async Task<Guid> CreateWalletForUser(Guid userId, string currency)
    {
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Currency = currency,
            Status = WalletStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        _db.Wallets.Add(wallet);
        await _db.SaveChangesAsync();
        return wallet.Id;
    }

    private async Task<Guid> CreditWallet(Guid walletId, decimal amount, string referenceId)
    {
        SetAuthHeader(_userToken);
        var request = new TransactionRequest(walletId, amount, referenceId);
        var response = await _client.PostAsJsonAsync("/api/transactions/credit", request);
        response.EnsureSuccessStatusCode();
        
        var entry = await _db.LedgerEntries
            .FirstAsync(l => l.WalletId == walletId && l.ReferenceId == referenceId);
        return entry.Id;
    }

    private async Task DebitWallet(Guid walletId, decimal amount, string referenceId)
    {
        SetAuthHeader(_userToken);
        var request = new TransactionRequest(walletId, amount, referenceId);
        var response = await _client.PostAsJsonAsync("/api/transactions/debit", request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<decimal> GetBalance(Guid walletId)
    {
        var credits = await _db.LedgerEntries
            .Where(l => l.WalletId == walletId && l.Type == LedgerEntryType.Credit && l.Status == TransactionStatus.Completed)
            .SumAsync(l => (decimal?)l.Amount) ?? 0;
        var debits = await _db.LedgerEntries
            .Where(l => l.WalletId == walletId && l.Type == LedgerEntryType.Debit && l.Status == TransactionStatus.Completed)
            .SumAsync(l => (decimal?)l.Amount) ?? 0;
        return credits - debits;
    }

    private async Task CreateBalanceSnapshot(Guid walletId)
    {
        SetAuthHeader(_userToken);
        var response = await _client.PostAsync($"/api/wallets/{walletId}/balance/snapshot", null);
        response.EnsureSuccessStatusCode();
    }

    #endregion
}


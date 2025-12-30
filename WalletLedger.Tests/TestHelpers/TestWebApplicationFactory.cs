using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using WalletLedger.Api;
using WalletLedger.Api.Data;

namespace WalletLedger.Tests.TestHelpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public TestWebApplicationFactory()
    {
        // Use shared cache so multiple connections (if created) can access the same in-memory database
        _connection = new SqliteConnection("DataSource=:memory:;Cache=Shared");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", "ThisIsATestKeyForJWTTokenGeneration12345678901234567890" },
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" },
                { "Jwt:AccessTokenExpiryMinutes", "60" },
                { "Jwt:RefreshTokenExpiryDays", "30" }
            });
        });

        builder.ConfigureServices(services =>
        {
            // In Testing environment, Program.cs doesn't register the DbContext, so we register it here with SQLite
            services.AddDbContext<WalletLedgerDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Add health checks with SQLite DbContext
            services.AddHealthChecks()
                .AddDbContextCheck<WalletLedgerDbContext>("database");

            // Ensure in-memory cache is used
            var cacheDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDistributedCache));
            if (cacheDescriptor != null && cacheDescriptor.ImplementationType != typeof(MemoryDistributedCache))
            {
                services.Remove(cacheDescriptor);
            }
            services.AddDistributedMemoryCache();
        });

        builder.UseEnvironment("Testing");
    }

    protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        
        // Ensure database is created after host is built
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WalletLedgerDbContext>();
            db.Database.EnsureCreated();
        }
        
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
        }
        base.Dispose(disposing);
    }
}


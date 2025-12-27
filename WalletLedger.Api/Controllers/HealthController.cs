using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Diagnostics;
using WalletLedger.Api.Data;

namespace WalletLedger.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly WalletLedgerDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        WalletLedgerDbContext db,
        IDistributedCache cache,
        ILogger<HealthController> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var healthStatus = new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Uptime = GetUptime(),
            Dependencies = new
            {
                Database = await CheckDatabaseHealthAsync(),
                Cache = await CheckCacheHealthAsync()
            }
        };

        var isHealthy = healthStatus.Dependencies.Database.Status == "Healthy" &&
                       healthStatus.Dependencies.Cache.Status == "Healthy";

        return isHealthy
            ? Ok(healthStatus)
            : StatusCode(503, healthStatus);
    }

    [Authorize(Policy = "AdminHealth")]
    [HttpGet("detailed")]
    public async Task<IActionResult> GetDetailedHealth()
    {
        var stopwatch = Stopwatch.StartNew();

        var databaseHealth = await CheckDatabaseHealthAsync();
        var cacheHealth = await CheckCacheHealthAsync();

        stopwatch.Stop();

        var healthStatus = new
        {
            Status = databaseHealth.Status == "Healthy" && cacheHealth.Status == "Healthy" ? "Healthy" : "Unhealthy",
            Timestamp = DateTime.UtcNow,
            Uptime = GetUptime(),
            ResponseTime = stopwatch.ElapsedMilliseconds,
            Dependencies = new
            {
                Database = databaseHealth,
                Cache = cacheHealth
            },
            System = new
            {
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet,
                GCMemory = GC.GetTotalMemory(false)
            }
        };

        var isHealthy = healthStatus.Status == "Healthy";
        return isHealthy
            ? Ok(healthStatus)
            : StatusCode(503, healthStatus);
    }

    [Authorize(Policy = "AdminHealth")]
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        var metrics = new
        {
            Timestamp = DateTime.UtcNow,
            Database = new
            {
                TotalWallets = await _db.Wallets.CountAsync(),
                TotalTransactions = await _db.LedgerEntries.CountAsync(),
                TotalUsers = await _db.Users.CountAsync(),
                TotalAuditLogs = await _db.AuditLogs.CountAsync()
            },
            System = new
            {
                Uptime = GetUptime(),
                Memory = new
                {
                    WorkingSet = Environment.WorkingSet,
                    GCMemory = GC.GetTotalMemory(false),
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2)
                },
                Threads = Process.GetCurrentProcess().Threads.Count,
                Handles = Process.GetCurrentProcess().HandleCount
            }
        };

        return Ok(metrics);
    }

    private async Task<dynamic> CheckDatabaseHealthAsync()
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            if (!canConnect)
            {
                return new { Status = "Unhealthy", Message = "Cannot connect to database" };
            }

            // Try a simple query
            await _db.Database.ExecuteSqlRawAsync("SELECT 1");

            return new { Status = "Healthy", Message = "Database is accessible" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return new { Status = "Unhealthy", Message = ex.Message };
        }
    }

    private async Task<dynamic> CheckCacheHealthAsync()
    {
        try
        {
            var testKey = $"health_check_{Guid.NewGuid()}";
            var testValue = "test";

            await _cache.SetStringAsync(testKey, testValue, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
            });

            var retrievedValue = await _cache.GetStringAsync(testKey);

            if (retrievedValue == testValue)
            {
                await _cache.RemoveAsync(testKey);
                return new { Status = "Healthy", Message = "Cache is accessible" };
            }

            return new { Status = "Unhealthy", Message = "Cache read/write test failed" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache health check failed");
            return new { Status = "Unhealthy", Message = ex.Message };
        }
    }

    private static string GetUptime()
    {
        var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
        return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
    }
}


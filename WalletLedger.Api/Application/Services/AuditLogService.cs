using Microsoft.EntityFrameworkCore;
using WalletLedger.Api.Application.Interfaces;
using WalletLedger.Api.Data;
using WalletLedger.Api.Domain.Entities;

namespace WalletLedger.Api.Application.Services;

public class AuditLogService : IAuditLogService
{
    private readonly WalletLedgerDbContext _db;

    public AuditLogService(WalletLedgerDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId = null,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };

        _db.AuditLogs.Add(auditLog);
        await _db.SaveChangesAsync();
    }
}




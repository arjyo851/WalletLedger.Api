namespace WalletLedger.Api.Application.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId = null,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null);
}







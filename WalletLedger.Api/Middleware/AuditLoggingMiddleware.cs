using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using WalletLedger.Api.Application.Interfaces;

namespace WalletLedger.Api.Middleware;

public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditLogService auditLogService)
    {
        // Skip audit logging for health checks and swagger
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                     context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);

        Guid? userIdGuid = null;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var parsedUserId))
        {
            userIdGuid = parsedUserId;
        }

        var action = $"{context.Request.Method} {context.Request.Path}";
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers["User-Agent"].ToString();

        try
        {
            await _next(context);

            // Log successful operations (2xx status codes)
            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                // Determine entity type from path
                var entityType = GetEntityTypeFromPath(context.Request.Path);
                var entityId = GetEntityIdFromPath(context.Request.Path);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await auditLogService.LogAsync(
                            userIdGuid,
                            action,
                            entityType,
                            entityId,
                            $"Status: {context.Response.StatusCode}",
                            ipAddress,
                            userAgent
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to write audit log");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            // Log failed operations
            _ = Task.Run(async () =>
            {
                try
                {
                    var entityType = GetEntityTypeFromPath(context.Request.Path);
                    var entityId = GetEntityIdFromPath(context.Request.Path);

                    await auditLogService.LogAsync(
                        userIdGuid,
                        action,
                        entityType,
                        entityId,
                        $"Error: {ex.Message}",
                        ipAddress,
                        userAgent
                    );
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to write audit log for error");
                }
            });

            throw;
        }
    }

    private static string GetEntityTypeFromPath(PathString path)
    {
        if (path.Value?.Contains("/wallets") == true) return "Wallet";
        if (path.Value?.Contains("/transactions") == true) return "Transaction";
        if (path.Value?.Contains("/auth") == true) return "Auth";
        return "Unknown";
    }

    private static Guid? GetEntityIdFromPath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments == null || segments.Length < 2) return null;

        // Try to find a GUID in the path segments
        foreach (var segment in segments)
        {
            if (Guid.TryParse(segment, out var guid))
            {
                return guid;
            }
        }

        return null;
    }
}



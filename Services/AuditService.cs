using System.Text.Json;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Http;

namespace AcxiomCRM.Services
{
    public interface IAuditService
    {
        Task LogAsync(string action, string entityName, string? recordId, object? oldValue, object? newValue, string? details = null, string result = "Success");
    }

    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string action, string entityName, string? recordId, object? oldValue, object? newValue, string? details = null, string result = "Success")
        {
            var httpContext = _httpContextAccessor.HttpContext;
            string? userId = null;
            string? userName = null;
            string? ipAddress = null;

            if (httpContext != null)
            {
                userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                userName = httpContext.User?.Identity?.Name;
                ipAddress = httpContext.Connection?.RemoteIpAddress?.ToString();
            }

            string? oldJson = oldValue != null ? (oldValue is string s1 ? s1 : JsonSerializer.Serialize(oldValue)) : null;
            string? newJson = newValue != null ? (newValue is string s2 ? s2 : JsonSerializer.Serialize(newValue)) : null;

            var log = new AuditLog
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityName = entityName,
                RecordId = recordId,
                OldValue = oldJson,
                NewValue = newJson,
                CreatedDate = DateTime.UtcNow,
                IpAddress = ipAddress,
                Result = result,
                Details = details
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}

using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class AuditLogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? userSearch, string? module, string? action, DateTime? startDate, DateTime? endDate, int page = 1)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(userSearch))
            {
                string s = userSearch.Trim();
                query = query.Where(a => (a.UserName != null && a.UserName.Contains(s)) || (a.UserId != null && a.UserId.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(module))
            {
                query = query.Where(a => a.EntityName == module);
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(a => a.Action == action);
            }

            if (startDate.HasValue)
            {
                query = query.Where(a => a.CreatedDate >= startDate.Value.ToUniversalTime());
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.CreatedDate <= endDate.Value.ToUniversalTime().AddDays(1));
            }

            int pageSize = 20;
            int totalCount = await query.CountAsync();
            var logs = await query.OrderByDescending(a => a.CreatedDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.UserSearch = userSearch;
            ViewBag.Module = module;
            ViewBag.Action = action;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            return View(logs);
        }

        public async Task<IActionResult> Details(int id)
        {
            var log = await _context.AuditLogs.FindAsync(id);
            if (log == null) return NotFound();
            return View(log);
        }
    }
}

using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ActivityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;

        public ActivityController(ApplicationDbContext context, IUserService userService, IAuditService auditService)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index(string? type, string? status, DateTime? date, int page = 1)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var query = _context.Activities
                .Include(a => a.Customer)
                .Include(a => a.Lead)
                .Include(a => a.Opportunity)
                .Include(a => a.Assignee)
                .AsQueryable();

            if (!isAdmin && !isManager)
            {
                query = query.Where(a => a.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(a => a.ActivityType == type);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            if (date.HasValue)
            {
                query = query.Where(a => a.ActivityDate.Date == date.Value.Date);
            }

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            var activities = await query.OrderByDescending(a => a.ActivityDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.Type = type;
            ViewBag.Status = status;
            ViewBag.Date = date?.ToString("yyyy-MM-dd");

            return View(activities);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null, int? opportunityId = null)
        {
            await PopulateDropDowns(customerId, leadId, opportunityId);
            return View(new Activity
            {
                CustomerId = customerId,
                LeadId = leadId,
                OpportunityId = opportunityId,
                ActivityDate = DateTime.UtcNow,
                ActivityType = ActivityTypes.Call,
                Status = "Completed"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Activity activity)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            if (!ActivityTypes.All.Contains(activity.ActivityType))
            {
                ModelState.AddModelError("ActivityType", "Invalid activity type.");
            }

            if (ModelState.IsValid)
            {
                activity.CreatedDate = DateTime.UtcNow;
                if (string.IsNullOrEmpty(activity.AssignedTo))
                {
                    activity.AssignedTo = user.Id;
                }

                _context.Activities.Add(activity);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Create", "Activity", activity.ActivityId.ToString(), null, activity, $"Created {activity.ActivityType} activity: {activity.Subject}");

                TempData["SuccessMessage"] = $"Activity '{activity.Subject}' recorded successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropDowns(activity.CustomerId, activity.LeadId, activity.OpportunityId, activity.AssignedTo);
            return View(activity);
        }

        private async Task PopulateDropDowns(int? selectedCustomer = null, int? selectedLead = null, int? selectedOpp = null, string? selectedUser = null)
        {
            ViewBag.CustomersList = new SelectList(await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync(), "CustomerId", "CustomerName", selectedCustomer);
            ViewBag.LeadsList = new SelectList(await _context.Leads.OrderBy(l => l.LeadName).ToListAsync(), "LeadId", "LeadName", selectedLead);
            ViewBag.OpportunitiesList = new SelectList(await _context.Opportunities.OrderBy(o => o.OpportunityName).ToListAsync(), "OpportunityId", "OpportunityName", selectedOpp);
            ViewBag.UsersList = new SelectList(await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync(), "Id", "FullName", selectedUser);
        }
    }
}

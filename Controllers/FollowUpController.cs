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
    public class FollowUpController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;

        public FollowUpController(ApplicationDbContext context, IUserService userService, IAuditService auditService)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index(string? status, string? type, DateTime? date, int page = 1)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var query = _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Include(f => f.Assignee)
                .AsQueryable();

            if (!isAdmin && !isManager)
            {
                query = query.Where(f => f.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(f => f.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(f => f.FollowUpType == type);
            }

            if (date.HasValue)
            {
                query = query.Where(f => f.FollowUpDate.Date == date.Value.Date);
            }

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            var followUps = await query.OrderBy(f => f.FollowUpDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.Status = status;
            ViewBag.Type = type;
            ViewBag.Date = date?.ToString("yyyy-MM-dd");

            return View(followUps);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null, int? opportunityId = null)
        {
            await PopulateDropDowns(customerId, leadId, opportunityId);
            return View(new FollowUp
            {
                CustomerId = customerId,
                LeadId = leadId,
                OpportunityId = opportunityId,
                FollowUpDate = DateTime.UtcNow.AddDays(1).Date.AddHours(10), // Tomorrow at 10 AM
                Status = FollowUpStatuses.Planned
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FollowUp followUp)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            // Business rule: Follow-up date cannot be earlier than today for a new/planned follow-up
            if (followUp.FollowUpDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
            }

            if (ModelState.IsValid)
            {
                followUp.CreatedDate = DateTime.UtcNow;
                if (string.IsNullOrEmpty(followUp.AssignedTo))
                {
                    followUp.AssignedTo = user.Id;
                }

                _context.FollowUps.Add(followUp);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Create", "FollowUp", followUp.FollowUpId.ToString(), null, followUp, $"Scheduled follow-up '{followUp.Subject}'");

                TempData["SuccessMessage"] = $"Follow-up '{followUp.Subject}' scheduled successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropDowns(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedTo);
            return View(followUp);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, string? remarks)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound();

            if (!isAdmin && !isManager && followUp.AssignedTo != user.Id)
            {
                return Forbid();
            }

            var oldStatus = followUp.Status;
            followUp.Status = FollowUpStatuses.Completed;
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                followUp.Remarks = (string.IsNullOrEmpty(followUp.Remarks) ? "" : followUp.Remarks + " | ") + $"Completed: {remarks}";
            }

            // Also create completed activity entry
            var activity = new Activity
            {
                ActivityType = followUp.FollowUpType,
                Subject = $"Follow-Up Completed: {followUp.Subject}",
                Description = followUp.Remarks,
                ActivityDate = DateTime.UtcNow,
                CustomerId = followUp.CustomerId,
                LeadId = followUp.LeadId,
                OpportunityId = followUp.OpportunityId,
                AssignedTo = followUp.AssignedTo,
                Status = "Completed",
                CreatedDate = DateTime.UtcNow
            };
            _context.Activities.Add(activity);

            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Complete", "FollowUp", followUp.FollowUpId.ToString(), new { Status = oldStatus }, new { Status = followUp.Status }, $"Marked follow-up '{followUp.Subject}' as completed");

            TempData["SuccessMessage"] = $"Follow-up marked as completed.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkMissed(int id)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound();

            var oldStatus = followUp.Status;
            followUp.Status = FollowUpStatuses.Missed;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Missed", "FollowUp", followUp.FollowUpId.ToString(), new { Status = oldStatus }, new { Status = followUp.Status }, $"Marked follow-up '{followUp.Subject}' as missed");

            TempData["SuccessMessage"] = "Follow-up marked as missed.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reschedule(int id, DateTime newDate, string? remarks)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            if (newDate.Date < DateTime.UtcNow.Date)
            {
                TempData["ErrorMessage"] = "New follow-up date cannot be earlier than today.";
                return RedirectToAction(nameof(Index));
            }

            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound();

            var oldDate = followUp.FollowUpDate;
            followUp.FollowUpDate = newDate;
            followUp.Status = FollowUpStatuses.Planned;
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                followUp.Remarks = (string.IsNullOrEmpty(followUp.Remarks) ? "" : followUp.Remarks + " | ") + $"Rescheduled to {newDate:yyyy-MM-dd}: {remarks}";
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Reschedule", "FollowUp", followUp.FollowUpId.ToString(), new { Date = oldDate }, new { Date = followUp.FollowUpDate }, $"Rescheduled follow-up '{followUp.Subject}'");

            TempData["SuccessMessage"] = "Follow-up rescheduled successfully.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropDowns(int? selectedCustomer = null, int? selectedLead = null, int? selectedOpp = null, string? selectedUser = null)
        {
            ViewBag.CustomersList = new SelectList(await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync(), "CustomerId", "CustomerName", selectedCustomer);
            ViewBag.LeadsList = new SelectList(await _context.Leads.Where(l => l.Status != LeadStatuses.Converted).OrderBy(l => l.LeadName).ToListAsync(), "LeadId", "LeadName", selectedLead);
            ViewBag.OpportunitiesList = new SelectList(await _context.Opportunities.Where(o => o.Status == "Open").OrderBy(o => o.OpportunityName).ToListAsync(), "OpportunityId", "OpportunityName", selectedOpp);
            ViewBag.UsersList = new SelectList(await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync(), "Id", "FullName", selectedUser);
        }
    }
}

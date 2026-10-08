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
    public class OpportunityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;

        public OpportunityController(ApplicationDbContext context, IUserService userService, IAuditService auditService)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index(string? search, string? stage, string? status, string? sortOrder, int page = 1)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var query = _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.Assignee)
                .AsQueryable();

            if (!isAdmin && !isManager)
            {
                query = query.Where(o => o.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(o => o.OpportunityName.Contains(s)
                                      || (o.Customer != null && o.Customer.CustomerName.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(stage))
            {
                query = query.Where(o => o.Stage == stage);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o => o.Status == status);
            }

            ViewBag.CurrentSort = sortOrder;
            ViewBag.NameSortParm = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewBag.AmountSortParm = sortOrder == "amount" ? "amount_desc" : "amount";
            ViewBag.DateSortParm = sortOrder == "date" ? "date_desc" : "date";

            query = sortOrder switch
            {
                "name_desc" => query.OrderByDescending(o => o.OpportunityName),
                "amount" => query.OrderBy(o => o.Amount),
                "amount_desc" => query.OrderByDescending(o => o.Amount),
                "date" => query.OrderBy(o => o.ExpectedCloseDate),
                "date_desc" => query.OrderByDescending(o => o.ExpectedCloseDate),
                _ => query.OrderByDescending(o => o.CreatedDate),
            };

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            var opportunities = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.Search = search;
            ViewBag.Stage = stage;
            ViewBag.Status = status;

            return View(opportunities);
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var opportunity = await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.Lead)
                .Include(o => o.Assignee)
                .Include(o => o.FollowUps)
                .Include(o => o.Activities)
                .FirstOrDefaultAsync(o => o.OpportunityId == id);

            if (opportunity == null) return NotFound();

            if (!isAdmin && !isManager && opportunity.AssignedTo != user.Id)
            {
                return Forbid();
            }

            ViewBag.AuditHistory = await _context.AuditLogs
                .Where(a => a.EntityName == "Opportunity" && a.RecordId == id.ToString())
                .OrderByDescending(a => a.CreatedDate)
                .Take(20)
                .ToListAsync();

            return View(opportunity);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? customerId = null)
        {
            await PopulateCustomersDropDownList(customerId);
            await PopulateUsersDropDownList();

            return View(new Opportunity
            {
                CustomerId = customerId,
                Stage = OpportunityStages.Qualification,
                Probability = 20,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Opportunity opportunity)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            // Server-side business validations
            if (opportunity.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
            }

            if (opportunity.Probability < 0 || opportunity.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
            }

            if (opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date && opportunity.Stage != OpportunityStages.Won && opportunity.Stage != OpportunityStages.Lost)
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past for an active Opportunity.");
            }

            if (ModelState.IsValid)
            {
                opportunity.CreatedDate = DateTime.UtcNow;
                if (string.IsNullOrEmpty(opportunity.AssignedTo))
                {
                    opportunity.AssignedTo = user.Id;
                }

                if (opportunity.Stage == OpportunityStages.Won) opportunity.Status = "Won";
                else if (opportunity.Stage == OpportunityStages.Lost) opportunity.Status = "Lost";
                else opportunity.Status = "Open";

                _context.Opportunities.Add(opportunity);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Create", "Opportunity", opportunity.OpportunityId.ToString(), null, opportunity, $"Created opportunity {opportunity.OpportunityName}");

                TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' created successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateCustomersDropDownList(opportunity.CustomerId);
            await PopulateUsersDropDownList(opportunity.AssignedTo);
            return View(opportunity);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var opportunity = await _context.Opportunities.FindAsync(id);
            if (opportunity == null) return NotFound();

            if (!isAdmin && !isManager && opportunity.AssignedTo != user.Id)
            {
                return Forbid();
            }

            await PopulateCustomersDropDownList(opportunity.CustomerId);
            await PopulateUsersDropDownList(opportunity.AssignedTo);
            return View(opportunity);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Opportunity opportunity)
        {
            if (id != opportunity.OpportunityId) return BadRequest();

            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var existing = await _context.Opportunities.AsNoTracking().FirstOrDefaultAsync(o => o.OpportunityId == id);
            if (existing == null) return NotFound();

            if (!isAdmin && !isManager && existing.AssignedTo != user.Id)
            {
                return Forbid();
            }

            if (opportunity.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
            }

            if (opportunity.Probability < 0 || opportunity.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
            }

            if (opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date && opportunity.Stage != OpportunityStages.Won && opportunity.Stage != OpportunityStages.Lost)
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past for an active Opportunity.");
            }

            if (ModelState.IsValid)
            {
                if (opportunity.Stage == OpportunityStages.Won) opportunity.Status = "Won";
                else if (opportunity.Stage == OpportunityStages.Lost) opportunity.Status = "Lost";
                else opportunity.Status = "Open";

                _context.Update(opportunity);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Update", "Opportunity", opportunity.OpportunityId.ToString(), existing, opportunity, $"Updated opportunity {opportunity.OpportunityName} to stage {opportunity.Stage}");

                TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateCustomersDropDownList(opportunity.CustomerId);
            await PopulateUsersDropDownList(opportunity.AssignedTo);
            return View(opportunity);
        }

        private async Task PopulateCustomersDropDownList(object? selectedCustomer = null)
        {
            var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
            ViewBag.CustomersList = new SelectList(customers, "CustomerId", "CustomerName", selectedCustomer);
        }

        private async Task PopulateUsersDropDownList(object? selectedUser = null)
        {
            var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.UsersList = new SelectList(users, "Id", "FullName", selectedUser);
        }
    }
}

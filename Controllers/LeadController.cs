using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class LeadController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;

        public LeadController(ApplicationDbContext context, IUserService userService, IAuditService auditService)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index(string? search, string? status, string? assignedTo, string? sortOrder, int page = 1)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var query = _context.Leads.Include(l => l.Assignee).AsQueryable();

            if (!isAdmin && !isManager)
            {
                query = query.Where(l => l.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(l => l.LeadName.Contains(s)
                                      || l.Email.Contains(s)
                                      || l.Phone.Contains(s)
                                      || (l.CompanyName != null && l.CompanyName.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(l => l.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(assignedTo) && (isAdmin || isManager))
            {
                query = query.Where(l => l.AssignedTo == assignedTo);
            }

            ViewBag.CurrentSort = sortOrder;
            ViewBag.NameSortParm = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewBag.DateSortParm = sortOrder == "date" ? "date_desc" : "date";

            query = sortOrder switch
            {
                "name_desc" => query.OrderByDescending(l => l.LeadName),
                "date" => query.OrderBy(l => l.CreatedDate),
                "date_desc" => query.OrderByDescending(l => l.CreatedDate),
                _ => query.OrderByDescending(l => l.CreatedDate),
            };

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            var leads = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.AssignedTo = assignedTo;

            await PopulateUsersDropDownList(assignedTo);
            return View(leads);
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var lead = await _context.Leads
                .Include(l => l.Assignee)
                .Include(l => l.ConvertedCustomer)
                .Include(l => l.Opportunities)
                .Include(l => l.FollowUps)
                .Include(l => l.Activities)
                .FirstOrDefaultAsync(l => l.LeadId == id);

            if (lead == null) return NotFound();

            if (!isAdmin && !isManager && lead.AssignedTo != user.Id)
            {
                return Forbid();
            }

            ViewBag.AuditHistory = await _context.AuditLogs
                .Where(a => a.EntityName == "Lead" && a.RecordId == id.ToString())
                .OrderByDescending(a => a.CreatedDate)
                .Take(20)
                .ToListAsync();

            return View(lead);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateUsersDropDownList();
            return View(new Lead
            {
                LeadCode = $"LEAD-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                Status = LeadStatuses.New
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Lead lead)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            // Validate status is allowed
            if (!LeadStatuses.All.Contains(lead.Status))
            {
                ModelState.AddModelError("Status", "Invalid lead status selected.");
            }

            if (lead.ExpectedValue < 0)
            {
                ModelState.AddModelError("ExpectedValue", "Expected value cannot be negative.");
            }

            if (ModelState.IsValid)
            {
                lead.CreatedDate = DateTime.UtcNow;
                if (string.IsNullOrEmpty(lead.AssignedTo))
                {
                    lead.AssignedTo = user.Id;
                }

                _context.Leads.Add(lead);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Create", "Lead", lead.LeadId.ToString(), null, lead, $"Created lead {lead.LeadName}");

                TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' created successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateUsersDropDownList(lead.AssignedTo);
            return View(lead);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (!isAdmin && !isManager && lead.AssignedTo != user.Id)
            {
                return Forbid();
            }

            await PopulateUsersDropDownList(lead.AssignedTo);
            return View(lead);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Lead lead)
        {
            if (id != lead.LeadId) return BadRequest();

            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var existing = await _context.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.LeadId == id);
            if (existing == null) return NotFound();

            if (!isAdmin && !isManager && existing.AssignedTo != user.Id)
            {
                return Forbid();
            }

            if (!LeadStatuses.All.Contains(lead.Status))
            {
                ModelState.AddModelError("Status", "Invalid lead status selected.");
            }

            // Disallow changing status backwards if already converted
            if (existing.Status == LeadStatuses.Converted && lead.Status != LeadStatuses.Converted)
            {
                ModelState.AddModelError("Status", "A converted lead cannot be moved back to active status.");
            }

            if (ModelState.IsValid)
            {
                _context.Update(lead);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Update", "Lead", lead.LeadId.ToString(), existing, lead, $"Updated lead {lead.LeadName}");

                TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateUsersDropDownList(lead.AssignedTo);
            return View(lead);
        }

        [HttpGet]
        public async Task<IActionResult> Convert(int id)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (lead.Status == LeadStatuses.Converted)
            {
                TempData["ErrorMessage"] = "Lead has already been converted.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var model = new ConvertLeadViewModel
            {
                LeadId = lead.LeadId,
                LeadName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                OpportunityName = $"{lead.CompanyName ?? lead.LeadName} - Expansion",
                OpportunityAmount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 10000m,
                Probability = 25,
                ExpectedCloseDate = DateTime.UtcNow.AddMonths(1)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Convert(ConvertLeadViewModel model)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var lead = await _context.Leads.FindAsync(model.LeadId);
            if (lead == null) return NotFound();

            if (lead.Status == LeadStatuses.Converted)
            {
                TempData["ErrorMessage"] = "Lead has already been converted.";
                return RedirectToAction(nameof(Details), new { id = lead.LeadId });
            }

            if (model.CreateOpportunity)
            {
                if (string.IsNullOrWhiteSpace(model.OpportunityName))
                {
                    ModelState.AddModelError("OpportunityName", "Opportunity Name is required.");
                }
                if (model.OpportunityAmount <= 0)
                {
                    ModelState.AddModelError("OpportunityAmount", "Opportunity Amount must be greater than 0.");
                }
                if (model.Probability < 0 || model.Probability > 100)
                {
                    ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
                }
                if (model.ExpectedCloseDate.HasValue && model.ExpectedCloseDate.Value.Date < DateTime.UtcNow.Date)
                {
                    ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if customer with this email or phone exists or create new
            var existingCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == lead.Email.ToLower() || c.Phone == lead.Phone);
            Customer customer;
            if (existingCustomer != null)
            {
                customer = existingCustomer;
            }
            else
            {
                customer = new Customer
                {
                    CustomerCode = $"CUST-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                    CustomerName = lead.LeadName,
                    Email = lead.Email,
                    Phone = lead.Phone,
                    CompanyName = lead.CompanyName,
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = user.Id,
                    OwnerId = lead.AssignedTo ?? user.Id
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Create", "Customer", customer.CustomerId.ToString(), null, customer, $"Created customer from Lead conversion: {customer.CustomerName}");
            }

            Opportunity? opportunity = null;
            if (model.CreateOpportunity)
            {
                opportunity = new Opportunity
                {
                    OpportunityName = model.OpportunityName!,
                    CustomerId = customer.CustomerId,
                    LeadId = lead.LeadId,
                    Amount = model.OpportunityAmount,
                    Stage = OpportunityStages.Qualification,
                    Probability = model.Probability,
                    ExpectedCloseDate = model.ExpectedCloseDate ?? DateTime.UtcNow.AddMonths(1),
                    Status = "Open",
                    CreatedDate = DateTime.UtcNow,
                    AssignedTo = lead.AssignedTo ?? user.Id
                };
                _context.Opportunities.Add(opportunity);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Create", "Opportunity", opportunity.OpportunityId.ToString(), null, opportunity, $"Created opportunity from Lead conversion: {opportunity.OpportunityName}");
            }

            // Update lead
            var oldStatus = lead.Status;
            lead.Status = LeadStatuses.Converted;
            lead.ConvertedCustomerId = customer.CustomerId;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Convert", "Lead", lead.LeadId.ToString(), new { Status = oldStatus }, new { Status = lead.Status, CustomerId = customer.CustomerId, OpportunityId = opportunity?.OpportunityId }, $"Lead converted to Customer #{customer.CustomerId}");

            TempData["SuccessMessage"] = $"Lead successfully converted to Customer '{customer.CustomerName}'" + (opportunity != null ? $" and Opportunity '{opportunity.OpportunityName}'." : ".");
            return RedirectToAction("Details", "Customer", new { id = customer.CustomerId });
        }

        private async Task PopulateUsersDropDownList(object? selectedUser = null)
        {
            var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.UsersList = new SelectList(users, "Id", "FullName", selectedUser);
        }
    }
}

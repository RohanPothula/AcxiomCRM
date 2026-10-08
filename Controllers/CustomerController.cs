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
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;

        public CustomerController(ApplicationDbContext context, IUserService userService, IAuditService auditService)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index(string? search, string? status, string? sortOrder, int page = 1)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var query = _context.Customers.Include(c => c.Owner).AsQueryable();

            if (!isAdmin && !isManager)
            {
                query = query.Where(c => c.OwnerId == user.Id || c.CreatedBy == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(c => c.CustomerName.Contains(s)
                                      || c.Email.Contains(s)
                                      || c.Phone.Contains(s)
                                      || (c.CompanyName != null && c.CompanyName.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(c => c.Status == status);
            }

            ViewBag.CurrentSort = sortOrder;
            ViewBag.NameSortParm = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewBag.DateSortParm = sortOrder == "date" ? "date_desc" : "date";

            query = sortOrder switch
            {
                "name_desc" => query.OrderByDescending(c => c.CustomerName),
                "date" => query.OrderBy(c => c.CreatedDate),
                "date_desc" => query.OrderByDescending(c => c.CreatedDate),
                _ => query.OrderBy(c => c.CustomerName),
            };

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            var customers = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.Search = search;
            ViewBag.Status = status;

            return View(customers);
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var customer = await _context.Customers
                .Include(c => c.Owner)
                .Include(c => c.Creator)
                .Include(c => c.Opportunities)
                .Include(c => c.FollowUps)
                .Include(c => c.Activities)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return NotFound();

            if (!isAdmin && !isManager && customer.OwnerId != user.Id && customer.CreatedBy != user.Id)
            {
                return Forbid();
            }

            // Customer history: audit logs
            ViewBag.AuditHistory = await _context.AuditLogs
                .Where(a => a.EntityName == "Customer" && a.RecordId == id.ToString())
                .OrderByDescending(a => a.CreatedDate)
                .Take(20)
                .ToListAsync();

            return View(customer);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateUsersDropDownList();
            return View(new Customer { CustomerCode = $"CUST-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            // Duplicate checks: email and phone uniqueness
            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == customer.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "A customer with this email address already exists.");
            }

            if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone))
            {
                ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
            }

            if (string.IsNullOrWhiteSpace(customer.CustomerName))
            {
                ModelState.AddModelError("CustomerName", "Customer Name is required.");
            }

            if (ModelState.IsValid)
            {
                customer.CreatedDate = DateTime.UtcNow;
                customer.CreatedBy = user.Id;
                if (string.IsNullOrEmpty(customer.OwnerId))
                {
                    customer.OwnerId = user.Id;
                }

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync("Create", "Customer", customer.CustomerId.ToString(), null, customer, $"Created customer {customer.CustomerName}");

                TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' created successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateUsersDropDownList(customer.OwnerId);
            return View(customer);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            if (!isAdmin && !isManager && customer.OwnerId != user.Id && customer.CreatedBy != user.Id)
            {
                return Forbid();
            }

            await PopulateUsersDropDownList(customer.OwnerId);
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.CustomerId) return BadRequest();

            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var existing = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == id);
            if (existing == null) return NotFound();

            if (!isAdmin && !isManager && existing.OwnerId != user.Id && existing.CreatedBy != user.Id)
            {
                return Forbid();
            }

            // Uniqueness check excluding current record
            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Email.ToLower() == customer.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "Another customer is already registered with this email address.");
            }

            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Phone == customer.Phone))
            {
                ModelState.AddModelError("Phone", "Another customer is already registered with this phone number.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(customer);
                    await _context.SaveChangesAsync();

                    await _auditService.LogAsync("Update", "Customer", customer.CustomerId.ToString(), existing, customer, $"Updated customer {customer.CustomerName}");

                    TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Customers.AnyAsync(e => e.CustomerId == id))
                        return NotFound();
                    throw;
                }
            }

            await PopulateUsersDropDownList(customer.OwnerId);
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            if (!isAdmin && !isManager && customer.OwnerId != user.Id)
            {
                return Forbid();
            }

            var oldStatus = customer.Status;
            customer.Status = customer.Status == "Active" ? "Inactive" : "Active";
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Update", "Customer", customer.CustomerId.ToString(), new { Status = oldStatus }, new { Status = customer.Status }, $"Changed customer status to {customer.Status}");

            TempData["SuccessMessage"] = $"Customer status changed to {customer.Status}.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateUsersDropDownList(object? selectedUser = null)
        {
            var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.UsersList = new SelectList(users, "Id", "FullName", selectedUser);
        }
    }
}

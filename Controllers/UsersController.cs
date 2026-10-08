using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditService _auditService;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditService auditService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index(string? search, string? role, int page = 1)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(u => u.FullName.Contains(s) || u.Email!.Contains(s));
            }

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            var users = await query.OrderBy(u => u.FullName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var viewModels = new List<UserListViewModel>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var primaryRole = roles.FirstOrDefault() ?? "None";

                if (!string.IsNullOrEmpty(role) && primaryRole != role)
                {
                    continue;
                }

                viewModels.Add(new UserListViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? "",
                    Role = primaryRole,
                    IsActive = u.IsActive,
                    IsLockedOut = await _userManager.IsLockedOutAsync(u),
                    CreatedDate = u.CreatedDate
                });
            }

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.Search = search;
            ViewBag.Role = role;

            return View(viewModels);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Roles = new[] { Roles.Admin, Roles.Manager, Roles.SalesExecutive };
            return View(new CreateUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = new[] { Roles.Admin, Roles.Manager, Roles.SalesExecutive };
                return View(model);
            }

            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
            {
                ModelState.AddModelError("Email", "A user with this email address already exists.");
                ViewBag.Roles = new[] { Roles.Admin, Roles.Manager, Roles.SalesExecutive };
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                IsActive = model.IsActive,
                CreatedDate = DateTime.UtcNow,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                if (await _roleManager.RoleExistsAsync(model.Role))
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
                }

                await _auditService.LogAsync("Create", "User", user.Id, null, new { user.Email, user.FullName, Role = model.Role, user.IsActive }, $"Admin created user {user.Email}");

                TempData["SuccessMessage"] = $"User '{user.FullName}' created successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var err in result.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            ViewBag.Roles = new[] { Roles.Admin, Roles.Manager, Roles.SalesExecutive };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var isLocked = await _userManager.IsLockedOutAsync(user);

            var model = new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "",
                Role = roles.FirstOrDefault() ?? Roles.SalesExecutive,
                IsActive = user.IsActive,
                IsLockedOut = isLocked
            };

            ViewBag.Roles = new[] { Roles.Admin, Roles.Manager, Roles.SalesExecutive };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, EditUserViewModel model)
        {
            if (id != model.Id) return BadRequest();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Roles = new[] { Roles.Admin, Roles.Manager, Roles.SalesExecutive };
                return View(model);
            }

            var oldRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
            var oldState = new { user.FullName, user.Email, user.IsActive, Role = oldRole };

            user.FullName = model.FullName;
            user.IsActive = model.IsActive;

            // Handle unlock if toggled
            if (!model.IsLockedOut && await _userManager.IsLockedOutAsync(user))
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                await _userManager.ResetAccessFailedCountAsync(user);
                await _auditService.LogAsync("Security", "User", user.Id, null, null, $"Admin unlocked user {user.Email}");
            }

            // Role update
            if (oldRole != model.Role && await _roleManager.RoleExistsAsync(model.Role))
            {
                if (!string.IsNullOrEmpty(oldRole))
                {
                    await _userManager.RemoveFromRoleAsync(user, oldRole);
                }
                await _userManager.AddToRoleAsync(user, model.Role);
                await _auditService.LogAsync("Role Change", "User", user.Id, new { Role = oldRole }, new { Role = model.Role }, $"Admin changed role for {user.Email} to {model.Role}");
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _auditService.LogAsync("Update", "User", user.Id, oldState, new { user.FullName, user.Email, user.IsActive, Role = model.Role }, $"Admin updated user {user.Email}");

                TempData["SuccessMessage"] = $"User '{user.FullName}' updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var err in result.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            ViewBag.Roles = new[] { Roles.Admin, Roles.Manager, Roles.SalesExecutive };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var model = new ResetPasswordViewModel
            {
                UserId = user.Id,
                UserEmail = user.Email ?? ""
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                // Reset lockout count as well
                await _userManager.ResetAccessFailedCountAsync(user);

                await _auditService.LogAsync("Security", "User", user.Id, null, null, $"Admin reset password for user {user.Email}");

                TempData["SuccessMessage"] = $"Password for '{user.Email}' reset successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var err in result.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Prevent self-deactivation
            var currentUserId = _userManager.GetUserId(User);
            if (user.Id == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot deactivate your own administrative account.";
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            await _auditService.LogAsync("Update", "User", user.Id, new { IsActive = !user.IsActive }, new { user.IsActive }, $"Admin changed user active state to {user.IsActive}");

            TempData["SuccessMessage"] = $"User status set to {(user.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction(nameof(Index));
        }
    }
}

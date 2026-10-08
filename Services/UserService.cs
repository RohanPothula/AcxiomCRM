using System.Security.Claims;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Services
{
    public interface IUserService
    {
        string? GetCurrentUserId(ClaimsPrincipal user);
        Task<ApplicationUser?> GetCurrentUserAsync(ClaimsPrincipal user);
        Task<bool> IsAdminAsync(ClaimsPrincipal user);
        Task<bool> IsManagerAsync(ClaimsPrincipal user);
        Task<bool> IsSalesExecutiveAsync(ClaimsPrincipal user);
        Task<string> GetPrimaryRoleAsync(ApplicationUser user);
    }

    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public string? GetCurrentUserId(ClaimsPrincipal user)
        {
            return _userManager.GetUserId(user);
        }

        public async Task<ApplicationUser?> GetCurrentUserAsync(ClaimsPrincipal user)
        {
            return await _userManager.GetUserAsync(user);
        }

        public async Task<bool> IsAdminAsync(ClaimsPrincipal user)
        {
            var appUser = await _userManager.GetUserAsync(user);
            return appUser != null && await _userManager.IsInRoleAsync(appUser, Roles.Admin);
        }

        public async Task<bool> IsManagerAsync(ClaimsPrincipal user)
        {
            var appUser = await _userManager.GetUserAsync(user);
            return appUser != null && await _userManager.IsInRoleAsync(appUser, Roles.Manager);
        }

        public async Task<bool> IsSalesExecutiveAsync(ClaimsPrincipal user)
        {
            var appUser = await _userManager.GetUserAsync(user);
            return appUser != null && await _userManager.IsInRoleAsync(appUser, Roles.SalesExecutive);
        }

        public async Task<string> GetPrimaryRoleAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(Roles.Admin)) return Roles.Admin;
            if (roles.Contains(Roles.Manager)) return Roles.Manager;
            if (roles.Contains(Roles.SalesExecutive)) return Roles.SalesExecutive;
            return roles.FirstOrDefault() ?? "User";
        }
    }
}

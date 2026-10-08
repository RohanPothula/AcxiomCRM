using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;

        public ReportsController(ApplicationDbContext context, IUserService userService)
        {
            _context = context;
            _userService = userService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();
            var role = await _userService.GetPrimaryRoleAsync(user);

            ViewBag.UserRole = role;
            return View();
        }

        public async Task<IActionResult> Customers(string? status, DateTime? startDate, DateTime? endDate)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();
            var role = await _userService.GetPrimaryRoleAsync(user);

            var query = _context.Customers.Include(c => c.Owner).AsQueryable();

            if (role != Roles.Admin && role != Roles.Manager)
            {
                query = query.Where(c => c.OwnerId == user.Id || c.CreatedBy == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);
            if (startDate.HasValue) query = query.Where(c => c.CreatedDate >= startDate.Value.ToUniversalTime());
            if (endDate.HasValue) query = query.Where(c => c.CreatedDate <= endDate.Value.ToUniversalTime().AddDays(1));

            var list = await query.OrderByDescending(c => c.CreatedDate).Select(c => new CustomerReportItem
            {
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Status = c.Status,
                OwnerName = c.Owner != null ? c.Owner.FullName : "Unassigned",
                CreatedDate = c.CreatedDate
            }).ToListAsync();

            ViewBag.Status = status;
            return View(list);
        }

        public async Task<IActionResult> Leads(string? source, string? status)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();
            var role = await _userService.GetPrimaryRoleAsync(user);

            var query = _context.Leads.Include(l => l.Assignee).AsQueryable();

            if (role != Roles.Admin && role != Roles.Manager)
            {
                query = query.Where(l => l.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(source)) query = query.Where(l => l.Source == source);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.Status == status);

            var list = await query.OrderByDescending(l => l.CreatedDate).Select(l => new LeadReportItem
            {
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Status = l.Status,
                ExpectedValue = l.ExpectedValue,
                OwnerName = l.Assignee != null ? l.Assignee.FullName : "Unassigned",
                CreatedDate = l.CreatedDate,
                IsConverted = l.Status == LeadStatuses.Converted
            }).ToListAsync();

            ViewBag.Source = source;
            ViewBag.Status = status;
            return View(list);
        }

        public async Task<IActionResult> FollowUps(string? status, string? type)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();
            var role = await _userService.GetPrimaryRoleAsync(user);

            var query = _context.FollowUps
                .Include(f => f.Assignee)
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .AsQueryable();

            if (role != Roles.Admin && role != Roles.Manager)
            {
                query = query.Where(f => f.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(f => f.Status == status);
            if (!string.IsNullOrWhiteSpace(type)) query = query.Where(f => f.FollowUpType == type);

            var today = DateTime.UtcNow.Date;
            var list = await query.OrderBy(f => f.FollowUpDate).Select(f => new FollowUpReportItem
            {
                Subject = f.Subject,
                FollowUpType = f.FollowUpType,
                FollowUpDate = f.FollowUpDate,
                Status = f.Status,
                OwnerName = f.Assignee != null ? f.Assignee.FullName : "Unassigned",
                TargetName = f.Customer != null ? f.Customer.CustomerName : (f.Lead != null ? f.Lead.LeadName : "-"),
                IsOverdue = f.Status == FollowUpStatuses.Planned && f.FollowUpDate.Date < today
            }).ToListAsync();

            ViewBag.Status = status;
            ViewBag.Type = type;
            return View(list);
        }

        public async Task<IActionResult> Opportunities(string? stage, string? status)
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();
            var role = await _userService.GetPrimaryRoleAsync(user);

            var query = _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.Assignee)
                .AsQueryable();

            if (role != Roles.Admin && role != Roles.Manager)
            {
                query = query.Where(o => o.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(o => o.Stage == stage);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);

            var list = await query.OrderByDescending(o => o.CreatedDate).Select(o => new OpportunityReportItem
            {
                OpportunityName = o.OpportunityName,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : "Unknown",
                Stage = o.Stage,
                Amount = o.Amount,
                Probability = o.Probability,
                WeightedAmount = (o.Amount * o.Probability) / 100m,
                ExpectedCloseDate = o.ExpectedCloseDate,
                OwnerName = o.Assignee != null ? o.Assignee.FullName : "Unassigned"
            }).ToListAsync();

            ViewBag.Stage = stage;
            ViewBag.Status = status;
            return View(list);
        }

        public async Task<IActionResult> Pipeline()
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();
            var role = await _userService.GetPrimaryRoleAsync(user);

            var query = _context.Opportunities
                .Include(o => o.Assignee)
                .Where(o => o.Status == "Open" && o.Stage != OpportunityStages.Won && o.Stage != OpportunityStages.Lost)
                .AsQueryable();

            if (role != Roles.Admin && role != Roles.Manager)
            {
                query = query.Where(o => o.AssignedTo == user.Id);
            }

            var opps = await query.ToListAsync();

            var vm = new PipelineReportViewModel
            {
                TotalPipelineAmount = opps.Sum(o => o.Amount),
                TotalWeightedPipelineAmount = opps.Sum(o => (o.Amount * o.Probability) / 100m)
            };

            var stages = new[] { OpportunityStages.Qualification, OpportunityStages.Proposal, OpportunityStages.Negotiation };
            foreach (var stg in stages)
            {
                var stageOpps = opps.Where(o => o.Stage == stg).ToList();
                vm.StageWiseAmounts[stg] = stageOpps.Sum(o => o.Amount);
                vm.StageWiseWeightedAmounts[stg] = stageOpps.Sum(o => (o.Amount * o.Probability) / 100m);
            }

            var byOwner = opps.GroupBy(o => o.Assignee != null ? o.Assignee.FullName : "Unassigned");
            foreach (var group in byOwner)
            {
                vm.OwnerWiseAmounts[group.Key] = group.Sum(o => o.Amount);
            }

            return View(vm);
        }

        public async Task<IActionResult> Conversion()
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();
            var role = await _userService.GetPrimaryRoleAsync(user);

            var leadsQuery = _context.Leads.AsQueryable();
            var oppsQuery = _context.Opportunities.AsQueryable();

            if (role != Roles.Admin && role != Roles.Manager)
            {
                leadsQuery = leadsQuery.Where(l => l.AssignedTo == user.Id);
                oppsQuery = oppsQuery.Where(o => o.AssignedTo == user.Id);
            }

            int totalLeads = await leadsQuery.CountAsync();
            int convertedLeads = await leadsQuery.CountAsync(l => l.Status == LeadStatuses.Converted);

            int totalOpps = await oppsQuery.CountAsync();
            int wonOpps = await oppsQuery.CountAsync(o => o.Stage == OpportunityStages.Won || o.Status == "Won");
            int lostOpps = await oppsQuery.CountAsync(o => o.Stage == OpportunityStages.Lost || o.Status == "Lost");

            var vm = new ConversionReportViewModel
            {
                TotalLeads = totalLeads,
                ConvertedLeads = convertedLeads,
                UnconvertedLeads = totalLeads - convertedLeads,
                ConversionRate = totalLeads > 0 ? Math.Round((double)convertedLeads / totalLeads * 100.0, 1) : 0,
                TotalOpportunities = totalOpps,
                WonOpportunities = wonOpps,
                LostOpportunities = lostOpps,
                WinRate = totalOpps > 0 ? Math.Round((double)wonOpps / totalOpps * 100.0, 1) : 0
            };

            return View(vm);
        }

        [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
        public async Task<IActionResult> UserActivity()
        {
            var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
            var list = new List<UserActivityReportItem>();

            foreach (var u in users)
            {
                var role = (await _userService.GetPrimaryRoleAsync(u));
                var item = new UserActivityReportItem
                {
                    UserName = u.FullName,
                    Role = role,
                    CustomersCreated = await _context.Customers.CountAsync(c => c.OwnerId == u.Id || c.CreatedBy == u.Id),
                    LeadsAssigned = await _context.Leads.CountAsync(l => l.AssignedTo == u.Id),
                    OpportunitiesAssigned = await _context.Opportunities.CountAsync(o => o.AssignedTo == u.Id),
                    FollowUpsCompleted = await _context.FollowUps.CountAsync(f => f.AssignedTo == u.Id && f.Status == FollowUpStatuses.Completed),
                    ActivitiesLogged = await _context.Activities.CountAsync(a => a.AssignedTo == u.Id)
                };
                list.Add(item);
            }

            return View(list);
        }
    }
}

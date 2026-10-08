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
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;

        public DashboardController(ApplicationDbContext context, IUserService userService)
        {
            _context = context;
            _userService = userService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userService.GetCurrentUserAsync(User);
            if (user == null) return Challenge();

            var role = await _userService.GetPrimaryRoleAsync(user);
            bool isAdmin = role == Roles.Admin;
            bool isManager = role == Roles.Manager;
            string userId = user.Id;

            // Queries scoped by role
            var customersQuery = _context.Customers.AsNoTracking().AsQueryable();
            var leadsQuery = _context.Leads.AsNoTracking().AsQueryable();
            var oppsQuery = _context.Opportunities.AsNoTracking().AsQueryable();
            var followUpsQuery = _context.FollowUps.AsNoTracking().AsQueryable();
            var activitiesQuery = _context.Activities.AsNoTracking().AsQueryable();

            if (!isAdmin && !isManager)
            {
                // Sales Executive scoped to assigned records
                customersQuery = customersQuery.Where(c => c.OwnerId == userId || c.CreatedBy == userId);
                leadsQuery = leadsQuery.Where(l => l.AssignedTo == userId);
                oppsQuery = oppsQuery.Where(o => o.AssignedTo == userId);
                followUpsQuery = followUpsQuery.Where(f => f.AssignedTo == userId);
                activitiesQuery = activitiesQuery.Where(a => a.AssignedTo == userId);
            }

            var today = DateTime.UtcNow.Date;

            var model = new DashboardViewModel
            {
                UserRole = role,
                UserDisplayName = user.FullName ?? user.UserName ?? "User",
                TotalCustomers = await customersQuery.CountAsync(),
                TotalLeads = await leadsQuery.CountAsync(),
                OpenLeads = await leadsQuery.CountAsync(l => l.Status != LeadStatuses.Converted && l.Status != LeadStatuses.Lost && l.Status != LeadStatuses.Unqualified),
                TotalOpportunities = await oppsQuery.CountAsync(),
                OpenOpportunities = await oppsQuery.CountAsync(o => o.Status == "Open" && o.Stage != OpportunityStages.Won && o.Stage != OpportunityStages.Lost),
                WonOpportunities = await oppsQuery.CountAsync(o => o.Stage == OpportunityStages.Won || o.Status == "Won"),
                LostOpportunities = await oppsQuery.CountAsync(o => o.Stage == OpportunityStages.Lost || o.Status == "Lost"),
                TotalPipelineValue = await oppsQuery.Where(o => o.Status == "Open" && o.Stage != OpportunityStages.Won && o.Stage != OpportunityStages.Lost).SumAsync(o => (decimal?)o.Amount) ?? 0m,
                PendingFollowUps = await followUpsQuery.CountAsync(f => f.Status == FollowUpStatuses.Planned),
                OverdueFollowUps = await followUpsQuery.CountAsync(f => f.Status == FollowUpStatuses.Planned && f.FollowUpDate.Date < today)
            };

            // Calculate weighted pipeline
            var openOpps = await oppsQuery.Where(o => o.Status == "Open" && o.Stage != OpportunityStages.Won && o.Stage != OpportunityStages.Lost)
                .Select(o => new { o.Amount, o.Probability })
                .ToListAsync();
            model.WeightedPipelineValue = openOpps.Sum(o => (o.Amount * o.Probability) / 100m);

            // Lead status chart counts
            var leadStatuses = new[] { LeadStatuses.New, LeadStatuses.Contacted, LeadStatuses.Qualified, LeadStatuses.Lost, LeadStatuses.Converted };
            var leadCountsGrouped = await leadsQuery
                .GroupBy(l => l.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Status, g => g.Count);

            foreach (var status in leadStatuses)
            {
                model.LeadStatusCounts[status] = leadCountsGrouped.TryGetValue(status, out var count) ? count : 0;
            }

            // Opportunity pipeline chart counts
            var oppStages = new[] { OpportunityStages.Qualification, OpportunityStages.Proposal, OpportunityStages.Negotiation, OpportunityStages.Won, OpportunityStages.Lost };
            var oppCountsGrouped = await oppsQuery
                .GroupBy(o => o.Stage)
                .Select(g => new { Stage = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Stage, g => g.Count);

            foreach (var stage in oppStages)
            {
                model.OpportunityStageCounts[stage] = oppCountsGrouped.TryGetValue(stage, out var count) ? count : 0;
            }

            // Monthly Sales: won opportunities over last 6 months
            for (int i = 5; i >= 0; i--)
            {
                var targetMonth = DateTime.UtcNow.AddMonths(-i);
                var monthLabel = targetMonth.ToString("MMM yyyy");
                var sum = await oppsQuery
                    .Where(o => (o.Stage == OpportunityStages.Won || o.Status == "Won") && o.ExpectedCloseDate.Year == targetMonth.Year && o.ExpectedCloseDate.Month == targetMonth.Month)
                    .SumAsync(o => (decimal?)o.Amount) ?? 0m;
                model.MonthlySales[monthLabel] = sum;
            }

            // Upcoming Follow-ups (next 5)
            model.UpcomingFollowUps = await followUpsQuery
                .Where(f => f.Status == FollowUpStatuses.Planned)
                .OrderBy(f => f.FollowUpDate)
                .Take(5)
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .ToListAsync();

            // Recent Activities (last 5)
            model.RecentActivities = await activitiesQuery
                .OrderByDescending(a => a.ActivityDate)
                .Take(5)
                .Include(a => a.Customer)
                .Include(a => a.Lead)
                .ToListAsync();

            return View(model);
        }
    }
}

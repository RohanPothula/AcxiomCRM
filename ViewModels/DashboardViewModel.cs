using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalCustomers { get; set; }
        public int TotalLeads { get; set; }
        public int OpenLeads { get; set; }
        public int TotalOpportunities { get; set; }
        public int OpenOpportunities { get; set; }
        public int WonOpportunities { get; set; }
        public int LostOpportunities { get; set; }
        public decimal TotalPipelineValue { get; set; }
        public decimal WeightedPipelineValue { get; set; }
        public int PendingFollowUps { get; set; }
        public int OverdueFollowUps { get; set; }

        public string UserRole { get; set; } = string.Empty;
        public string UserDisplayName { get; set; } = string.Empty;

        // Chart Data
        public Dictionary<string, int> LeadStatusCounts { get; set; } = new();
        public Dictionary<string, int> OpportunityStageCounts { get; set; } = new();
        public Dictionary<string, decimal> MonthlySales { get; set; } = new();

        public List<FollowUp> UpcomingFollowUps { get; set; } = new();
        public List<Activity> RecentActivities { get; set; } = new();
    }
}

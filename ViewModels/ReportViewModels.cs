namespace AcxiomCRM.ViewModels
{
    public class CustomerReportItem
    {
        public string CustomerCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string Status { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    public class LeadReportItem
    {
        public string LeadCode { get; set; } = string.Empty;
        public string LeadName { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? Source { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal ExpectedValue { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public bool IsConverted { get; set; }
    }

    public class FollowUpReportItem
    {
        public string Subject { get; set; } = string.Empty;
        public string FollowUpType { get; set; } = string.Empty;
        public DateTime FollowUpDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? TargetName { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class OpportunityReportItem
    {
        public string OpportunityName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Stage { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Probability { get; set; }
        public decimal WeightedAmount { get; set; }
        public DateTime ExpectedCloseDate { get; set; }
        public string OwnerName { get; set; } = string.Empty;
    }

    public class PipelineReportViewModel
    {
        public Dictionary<string, decimal> StageWiseAmounts { get; set; } = new();
        public Dictionary<string, decimal> StageWiseWeightedAmounts { get; set; } = new();
        public Dictionary<string, decimal> OwnerWiseAmounts { get; set; } = new();
        public decimal TotalPipelineAmount { get; set; }
        public decimal TotalWeightedPipelineAmount { get; set; }
    }

    public class ConversionReportViewModel
    {
        public int TotalLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public int UnconvertedLeads { get; set; }
        public double ConversionRate { get; set; }
        public int TotalOpportunities { get; set; }
        public int WonOpportunities { get; set; }
        public int LostOpportunities { get; set; }
        public double WinRate { get; set; }
    }

    public class UserActivityReportItem
    {
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int CustomersCreated { get; set; }
        public int LeadsAssigned { get; set; }
        public int OpportunitiesAssigned { get; set; }
        public int FollowUpsCompleted { get; set; }
        public int ActivitiesLogged { get; set; }
    }
}

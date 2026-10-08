using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels
{
    public class ConvertLeadViewModel
    {
        public int LeadId { get; set; }
        public string LeadName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? CompanyName { get; set; }

        public bool CreateOpportunity { get; set; } = true;

        [Display(Name = "Opportunity Name")]
        public string? OpportunityName { get; set; }

        [Range(0.01, 1000000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        [Display(Name = "Opportunity Amount")]
        public decimal OpportunityAmount { get; set; }

        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        public int Probability { get; set; } = 25;

        [Display(Name = "Expected Close Date")]
        public DateTime? ExpectedCloseDate { get; set; }
    }
}

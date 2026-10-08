using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models
{
    public static class OpportunityStages
    {
        public const string Qualification = "Qualification";
        public const string Proposal = "Proposal";
        public const string Negotiation = "Negotiation";
        public const string Won = "Won";
        public const string Lost = "Lost";

        public static readonly string[] All = { Qualification, Proposal, Negotiation, Won, Lost };
    }

    public class Opportunity
    {
        public int OpportunityId { get; set; }

        [Required(ErrorMessage = "Opportunity Name is required.")]
        [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
        public string OpportunityName { get; set; } = string.Empty;

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int? LeadId { get; set; }
        public Lead? Lead { get; set; }

        [Range(0.01, 1000000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Stage is required.")]
        [StringLength(50)]
        public string Stage { get; set; } = OpportunityStages.Qualification;

        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        public int Probability { get; set; } = 20;

        [Required(ErrorMessage = "Expected Close Date is required.")]
        public DateTime ExpectedCloseDate { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Open"; // Open, Won, Lost

        [StringLength(100)]
        public string? Source { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [StringLength(450)]
        public string? AssignedTo { get; set; }
        public ApplicationUser? Assignee { get; set; }

        public decimal WeightedAmount => (Amount * Probability) / 100m;

        public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
        public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    }
}

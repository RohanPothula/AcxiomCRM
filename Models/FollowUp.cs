using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models
{
    public static class FollowUpStatuses
    {
        public const string Planned = "Planned";
        public const string Completed = "Completed";
        public const string Missed = "Missed";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All = { Planned, Completed, Missed, Cancelled };
    }

    public class FollowUp
    {
        public int FollowUpId { get; set; }

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int? LeadId { get; set; }
        public Lead? Lead { get; set; }

        public int? OpportunityId { get; set; }
        public Opportunity? Opportunity { get; set; }

        [StringLength(50)]
        public string? RelatedType { get; set; } // Customer, Lead, Opportunity

        public int? RelatedId { get; set; }

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Follow-up date is required.")]
        public DateTime FollowUpDate { get; set; }

        [Required(ErrorMessage = "Follow-up type is required.")]
        [StringLength(50)]
        public string FollowUpType { get; set; } = "Call"; // Call, Meeting, Email, Task

        [StringLength(1000)]
        public string? Remarks { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = FollowUpStatuses.Planned;

        [StringLength(450)]
        public string? AssignedTo { get; set; }
        public ApplicationUser? Assignee { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}

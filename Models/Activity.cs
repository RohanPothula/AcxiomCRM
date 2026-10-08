using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models
{
    public static class ActivityTypes
    {
        public const string Call = "Call";
        public const string Meeting = "Meeting";
        public const string Email = "Email";
        public const string Task = "Task";

        public static readonly string[] All = { Call, Meeting, Email, Task };
    }

    public class Activity
    {
        public int ActivityId { get; set; }

        [Required(ErrorMessage = "Activity type is required.")]
        [StringLength(50)]
        public string ActivityType { get; set; } = ActivityTypes.Call;

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
        public string Subject { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int? LeadId { get; set; }
        public Lead? Lead { get; set; }

        public int? OpportunityId { get; set; }
        public Opportunity? Opportunity { get; set; }

        [StringLength(450)]
        public string? AssignedTo { get; set; }
        public ApplicationUser? Assignee { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Completed"; // Planned, InProgress, Completed, Cancelled

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}

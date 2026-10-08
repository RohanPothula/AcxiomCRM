using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models
{
    public static class LeadStatuses
    {
        public const string New = "New";
        public const string Contacted = "Contacted";
        public const string Qualified = "Qualified";
        public const string Unqualified = "Unqualified";
        public const string Converted = "Converted";
        public const string Lost = "Lost";

        public static readonly string[] All = { New, Contacted, Qualified, Unqualified, Converted, Lost };
    }

    public class Lead
    {
        public int LeadId { get; set; }

        [Required]
        [StringLength(50)]
        public string LeadCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lead Name is required.")]
        [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
        public string LeadName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required.")]
        [RegularExpression(@"^[0-9+\-\s]{7,20}$", ErrorMessage = "Enter a valid phone number.")]
        [StringLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
        public string Phone { get; set; } = string.Empty;

        [StringLength(150)]
        public string? CompanyName { get; set; }

        [StringLength(100)]
        public string? Source { get; set; } // Website, Referral, Cold Call, Advertisement, Partner, Other

        [Required(ErrorMessage = "Lead status is required.")]
        [StringLength(50)]
        public string Status { get; set; } = LeadStatuses.New;

        [StringLength(30)]
        public string Priority { get; set; } = "Medium"; // Low, Medium, High

        [Range(0, 100000000, ErrorMessage = "Expected value must be between 0 and 100,000,000.")]
        public decimal ExpectedValue { get; set; } = 0;

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [StringLength(450)]
        public string? AssignedTo { get; set; }
        public ApplicationUser? Assignee { get; set; }

        public int? ConvertedCustomerId { get; set; }
        public Customer? ConvertedCustomer { get; set; }

        public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
        public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
        public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    }
}

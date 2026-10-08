using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Lead> Leads => Set<Lead>();
        public DbSet<Opportunity> Opportunities => Set<Opportunity>();
        public DbSet<FollowUp> FollowUps => Set<FollowUp>();
        public DbSet<Activity> Activities => Set<Activity>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Customer
            builder.Entity<Customer>(entity =>
            {
                entity.HasKey(c => c.CustomerId);
                entity.HasIndex(c => c.CustomerCode).IsUnique();
                entity.HasIndex(c => c.Email).IsUnique();
                entity.HasIndex(c => c.Phone).IsUnique();

                entity.HasOne(c => c.Creator)
                    .WithMany()
                    .HasForeignKey(c => c.CreatedBy)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(c => c.Owner)
                    .WithMany()
                    .HasForeignKey(c => c.OwnerId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Lead
            builder.Entity<Lead>(entity =>
            {
                entity.HasKey(l => l.LeadId);
                entity.HasIndex(l => l.LeadCode).IsUnique();
                entity.HasIndex(l => l.Email);
                entity.HasIndex(l => l.Status);

                entity.HasOne(l => l.Assignee)
                    .WithMany()
                    .HasForeignKey(l => l.AssignedTo)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(l => l.ConvertedCustomer)
                    .WithMany()
                    .HasForeignKey(l => l.ConvertedCustomerId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Opportunity
            builder.Entity<Opportunity>(entity =>
            {
                entity.HasKey(o => o.OpportunityId);
                entity.HasIndex(o => o.Stage);
                entity.HasIndex(o => o.Status);

                entity.Property(o => o.Amount)
                    .HasPrecision(18, 2);

                entity.HasOne(o => o.Customer)
                    .WithMany(c => c.Opportunities)
                    .HasForeignKey(o => o.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(o => o.Lead)
                    .WithMany(l => l.Opportunities)
                    .HasForeignKey(o => o.LeadId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(o => o.Assignee)
                    .WithMany()
                    .HasForeignKey(o => o.AssignedTo)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // FollowUp
            builder.Entity<FollowUp>(entity =>
            {
                entity.HasKey(f => f.FollowUpId);
                entity.HasIndex(f => f.FollowUpDate);
                entity.HasIndex(f => f.Status);

                entity.HasOne(f => f.Customer)
                    .WithMany(c => c.FollowUps)
                    .HasForeignKey(f => f.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(f => f.Lead)
                    .WithMany(l => l.FollowUps)
                    .HasForeignKey(f => f.LeadId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(f => f.Opportunity)
                    .WithMany(o => o.FollowUps)
                    .HasForeignKey(f => f.OpportunityId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(f => f.Assignee)
                    .WithMany()
                    .HasForeignKey(f => f.AssignedTo)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Activity
            builder.Entity<Activity>(entity =>
            {
                entity.HasKey(a => a.ActivityId);
                entity.HasIndex(a => a.ActivityDate);
                entity.HasIndex(a => a.ActivityType);

                entity.HasOne(a => a.Customer)
                    .WithMany(c => c.Activities)
                    .HasForeignKey(a => a.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Lead)
                    .WithMany(l => l.Activities)
                    .HasForeignKey(a => a.LeadId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Opportunity)
                    .WithMany(o => o.Activities)
                    .HasForeignKey(a => a.OpportunityId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Assignee)
                    .WithMany()
                    .HasForeignKey(a => a.AssignedTo)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // AuditLog
            builder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(a => a.AuditLogId);
                entity.HasIndex(a => a.UserId);
                entity.HasIndex(a => a.EntityName);
                entity.HasIndex(a => a.Action);
                entity.HasIndex(a => a.CreatedDate);
            });
        }
    }
}

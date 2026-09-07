using ArielVertex.Domain.Common;
using ArielVertex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectDocument> ProjectDocuments => Set<ProjectDocument>();
    public DbSet<ProjectCall> ProjectCalls => Set<ProjectCall>();
    public DbSet<ProjectComment> ProjectComments => Set<ProjectComment>();
    public DbSet<StatusUpdate> StatusUpdates => Set<StatusUpdate>();
    public DbSet<ReviewRequest> ReviewRequests => Set<ReviewRequest>();
    public DbSet<ReviewMeeting> ReviewMeetings => Set<ReviewMeeting>();
    public DbSet<CodeReview> CodeReviews => Set<CodeReview>();
    public DbSet<ProjectReview> ProjectReviews => Set<ProjectReview>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<FeedbackCategoryScore> FeedbackCategoryScores => Set<FeedbackCategoryScore>();
    public DbSet<PerformanceReport> PerformanceReports => Set<PerformanceReport>();
    public DbSet<PerformanceCategoryScore> PerformanceCategoryScores => Set<PerformanceCategoryScore>();
    public DbSet<ResourceRequest> ResourceRequests => Set<ResourceRequest>();
    public DbSet<ResourceRequestComment> ResourceRequestComments => Set<ResourceRequestComment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<MicrosoftSyncLog> MicrosoftSyncLogs => Set<MicrosoftSyncLog>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseSetting> ExpenseSettings => Set<ExpenseSetting>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillSetting> BillSettings => Set<BillSetting>();
    public DbSet<Pip> Pips => Set<Pip>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<MeetingMinute> MeetingMinutes => Set<MeetingMinute>();
    public DbSet<AppraisalCycle> AppraisalCycles => Set<AppraisalCycle>();
    public DbSet<Appraisal> Appraisals => Set<Appraisal>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<TrainingRecommendation> TrainingRecommendations => Set<TrainingRecommendation>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AppraisalCycle>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Appraisal>(e =>
        {
            e.HasIndex(x => new { x.CycleId, x.EmployeeId }).IsUnique();
            e.Property(x => x.SelfComments).HasMaxLength(4000);
            e.Property(x => x.ManagerComments).HasMaxLength(4000);
            e.Property(x => x.SelfRating).HasPrecision(3, 2);
            e.Property(x => x.ManagerRating).HasPrecision(3, 2);
            e.Property(x => x.FinalRating).HasPrecision(3, 2);
            e.HasOne(x => x.Cycle).WithMany(c => c.Appraisals).HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Goal>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(160);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Category).HasMaxLength(80);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedBy).WithMany().HasForeignKey(x => x.AssignedById).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Cycle).WithMany(c => c.Goals).HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Promotion>(e =>
        {
            e.Property(x => x.CurrentDesignation).HasMaxLength(120);
            e.Property(x => x.ProposedDesignation).HasMaxLength(120);
            e.Property(x => x.Justification).HasMaxLength(2000);
            e.Property(x => x.DecisionNote).HasMaxLength(2000);
            e.Property(x => x.CurrentSalary).HasPrecision(14, 2);
            e.Property(x => x.ProposedSalary).HasPrecision(14, 2);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RecommendedBy).WithMany().HasForeignKey(x => x.RecommendedById).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<TrainingRecommendation>(e =>
        {
            e.Property(x => x.SkillGap).HasMaxLength(160);
            e.Property(x => x.RecommendedTraining).HasMaxLength(200);
            e.Property(x => x.Source).HasMaxLength(20);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Name).HasMaxLength(120);
            e.Property(u => u.Email).HasMaxLength(160);
            e.Property(u => u.EmployeeCode).HasMaxLength(20);
            e.Property(u => u.Designation).HasMaxLength(120);
            e.Property(u => u.Skills).HasMaxLength(600);
            e.HasOne(u => u.Department).WithMany(d => d.Members)
                .HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(u => u.Manager).WithMany(m => m.DirectReports)
                .HasForeignKey(u => u.ManagerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Project>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.Code).HasMaxLength(40);
            e.Property(p => p.Name).HasMaxLength(160);
        });

        b.Entity<ProjectMember>(e =>
        {
            e.HasIndex(m => new { m.ProjectId, m.UserId }).IsUnique();
            e.HasOne(m => m.Project).WithMany(p => p.Members)
                .HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.User).WithMany(u => u.ProjectMemberships)
                .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProjectDocument>(e =>
            e.HasOne(d => d.Project).WithMany(p => p.Documents)
                .HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Cascade));

        b.Entity<ProjectCall>(e =>
            e.HasOne(c => c.Project).WithMany(p => p.Calls)
                .HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Cascade));

        b.Entity<ProjectComment>(e =>
        {
            e.HasIndex(c => c.ProjectId);
            e.HasOne(c => c.Project).WithMany().HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StatusUpdate>(e =>
        {
            e.HasIndex(s => new { s.ProjectId, s.UserId, s.UpdateDate });
            e.Property(s => s.HoursSpent).HasPrecision(5, 2);
            e.HasOne(s => s.Project).WithMany(p => p.StatusUpdates)
                .HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.User).WithMany()
                .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ReviewRequest>(e =>
        {
            e.HasOne(r => r.Project).WithMany().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.SubjectUser).WithMany().HasForeignKey(r => r.SubjectUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.RequestedBy).WithMany().HasForeignKey(r => r.RequestedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.AssignedTo).WithMany().HasForeignKey(r => r.AssignedToId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.Meeting).WithOne(m => m.ReviewRequest!).HasForeignKey<ReviewMeeting>(m => m.ReviewRequestId);
            e.HasOne(r => r.CodeReview).WithOne(c => c.ReviewRequest!).HasForeignKey<CodeReview>(c => c.ReviewRequestId);
            e.HasOne(r => r.ProjectReview).WithOne(p => p.ReviewRequest!).HasForeignKey<ProjectReview>(p => p.ReviewRequestId);
        });

        b.Entity<Feedback>(e =>
        {
            e.HasOne(f => f.SubjectUser).WithMany().HasForeignKey(f => f.SubjectUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Author).WithMany().HasForeignKey(f => f.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Project).WithMany().HasForeignKey(f => f.ProjectId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(f => f.ApprovedBy).WithMany().HasForeignKey(f => f.ApprovedById).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<FeedbackCategoryScore>(e =>
            e.HasOne(c => c.Feedback).WithMany(f => f.CategoryScores)
                .HasForeignKey(c => c.FeedbackId).OnDelete(DeleteBehavior.Cascade));

        b.Entity<PerformanceReport>(e =>
        {
            e.Property(p => p.OverallScore).HasPrecision(5, 2);
            e.HasOne(p => p.SubjectUser).WithMany().HasForeignKey(p => p.SubjectUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.ApprovedBy).WithMany().HasForeignKey(p => p.ApprovedById).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<PerformanceCategoryScore>(e =>
        {
            e.Property(c => c.Weight).HasPrecision(5, 2);
            e.HasOne(c => c.PerformanceReport).WithMany(p => p.CategoryScores)
                .HasForeignKey(c => c.PerformanceReportId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ResourceRequest>(e =>
        {
            e.HasOne(r => r.Project).WithMany().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.RequestedBy).WithMany().HasForeignKey(r => r.RequestedById).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ResourceRequestComment>(e =>
            e.HasOne(c => c.ResourceRequest).WithMany(r => r.Comments)
                .HasForeignKey(c => c.ResourceRequestId).OnDelete(DeleteBehavior.Cascade));

        b.Entity<Notification>(e =>
        {
            e.HasIndex(n => new { n.RecipientId, n.IsRead });
            e.HasOne(n => n.Recipient).WithMany().HasForeignKey(n => n.RecipientId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.CreatedAt);
            e.HasOne(a => a.Actor).WithMany().HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Expense>(e =>
        {
            e.HasIndex(x => x.Status);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.HasOne(x => x.RaisedBy).WithMany().HasForeignKey(x => x.RaisedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Approver).WithMany().HasForeignKey(x => x.ApproverId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Bill>(e =>
        {
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DueDate);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.HasOne(x => x.RaisedBy).WithMany().HasForeignKey(x => x.RaisedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Approver).WithMany().HasForeignKey(x => x.ApproverId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Pip>(e =>
        {
            e.Property(x => x.TriggerScore).HasPrecision(5, 2);
            e.HasOne(x => x.SubjectUser).WithMany().HasForeignKey(x => x.SubjectUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<PlatformSetting>(e => e.HasIndex(x => x.Key).IsUnique());
        b.Entity<NotificationTemplate>(e => e.HasIndex(x => x.Key).IsUnique());

        b.Entity<MeetingMinute>(e =>
        {
            e.HasIndex(m => m.Status);
            e.HasOne(m => m.CreatedBy).WithMany().HasForeignKey(m => m.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.ApprovedBy).WithMany().HasForeignKey(m => m.ApprovedById).OnDelete(DeleteBehavior.SetNull);
        });
    }

    public override int SaveChanges()
    {
        Stamp();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        Stamp();
        return base.SaveChangesAsync(ct);
    }

    /// <summary>Maintain UTC timestamps centrally so no service has to remember.</summary>
    private void Stamp()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}

using System.Linq.Expressions;
using System.Reflection;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Person> People => Set<Person>();
    public DbSet<Parish> Parishes => Set<Parish>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateRetreat> CandidateRetreats => Set<CandidateRetreat>();
    public DbSet<CandidateFormationEvent> CandidateFormationEvents => Set<CandidateFormationEvent>();
    public DbSet<CanonicalMission> CanonicalMissions => Set<CanonicalMission>();
    public DbSet<Formator> Formators => Set<Formator>();
    public DbSet<ParishNeed> ParishNeeds => Set<ParishNeed>();
    public DbSet<ParishNeedAssignment> ParishNeedAssignments => Set<ParishNeedAssignment>();
    public DbSet<BudgetEntry> BudgetEntries => Set<BudgetEntry>();
    public DbSet<DokCase> DokCases => Set<DokCase>();
    public DbSet<CaseDocument> CaseDocuments => Set<CaseDocument>();
    public DbSet<PastoralNote> PastoralNotes => Set<PastoralNote>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingAttendee> MeetingAttendees => Set<MeetingAttendee>();
    public DbSet<Supervision> Supervisions => Set<Supervision>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();
    public DbSet<MailingCampaign> MailingCampaigns => Set<MailingCampaign>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Parish>(entity =>
        {
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.City).HasMaxLength(200);
        });

        builder.Entity<Person>(entity =>
        {
            entity.Property(p => p.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(p => p.LastName).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Email).HasMaxLength(256);
            entity.Property(p => p.Phone).HasMaxLength(50);

            entity.HasOne(p => p.Parish)
                .WithMany()
                .HasForeignKey(p => p.ParishId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Candidate>(entity =>
        {
            entity.HasOne(c => c.Person).WithMany().HasForeignKey(c => c.PersonId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CandidateRetreat>(entity =>
        {
            entity.HasOne(r => r.Candidate).WithMany(c => c.Retreats).HasForeignKey(r => r.CandidateId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(r => new { r.CandidateId, r.Year }).IsUnique();
            entity.HasQueryFilter(r => r.Candidate!.DeletedAtUtc == null);
        });

        builder.Entity<CandidateFormationEvent>(entity =>
        {
            entity.Property(e => e.PerformedByUserId).HasMaxLength(450);
            entity.Property(e => e.PerformedByEmail).HasMaxLength(256);
            entity.HasOne(e => e.Candidate).WithMany(c => c.Events).HasForeignKey(e => e.CandidateId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.CandidateId, e.Sequence }).IsUnique();
            entity.HasQueryFilter(e => e.Candidate!.DeletedAtUtc == null);
        });

        builder.Entity<CanonicalMission>(entity =>
        {
            entity.Property(m => m.ServicePlace).IsRequired().HasMaxLength(200);
            entity.Property(m => m.SupervisionGroup).HasMaxLength(100);
            entity.HasOne(m => m.Person).WithMany().HasForeignKey(m => m.PersonId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Formator>(entity =>
        {
            entity.Property(f => f.Function).IsRequired().HasMaxLength(200);
            entity.HasOne(f => f.Person).WithMany().HasForeignKey(f => f.PersonId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ParishNeed>(entity =>
        {
            entity.Property(n => n.Description).IsRequired().HasMaxLength(500);
            entity.HasOne(n => n.Parish).WithMany().HasForeignKey(n => n.ParishId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ParishNeedAssignment>(entity =>
        {
            entity.HasOne(a => a.ParishNeed).WithMany(n => n.Assignments).HasForeignKey(a => a.ParishNeedId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.Person).WithMany().HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(a => new { a.ParishNeedId, a.PersonId }).IsUnique();
            entity.HasQueryFilter(a => a.ParishNeed!.DeletedAtUtc == null && a.Person!.DeletedAtUtc == null);
        });

        builder.Entity<BudgetEntry>(entity =>
        {
            entity.Property(b => b.Description).IsRequired().HasMaxLength(300);
            entity.Property(b => b.Category).IsRequired().HasMaxLength(100);
            entity.Property(b => b.Amount).HasColumnType("decimal(18,2)");
        });

        builder.Entity<DokCase>(entity =>
        {
            entity.HasOne(c => c.Person).WithMany().HasForeignKey(c => c.PersonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.CatechistPerson).WithMany().HasForeignKey(c => c.CatechistPersonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.MentorPerson).WithMany().HasForeignKey(c => c.MentorPersonId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseDocument>(entity =>
        {
            entity.Property(d => d.Name).IsRequired().HasMaxLength(200);
            entity.HasOne(d => d.DokCase).WithMany().HasForeignKey(d => d.DokCaseId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PastoralNote>(entity =>
        {
            entity.Property(n => n.Content).IsRequired();
            entity.HasOne(n => n.DokCase).WithMany().HasForeignKey(n => n.DokCaseId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Meeting>(entity =>
        {
            entity.Property(m => m.GroupLabel).HasMaxLength(200);
            entity.HasOne(m => m.DokCase).WithMany().HasForeignKey(m => m.DokCaseId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Person>().WithMany().HasForeignKey(m => m.CatechistPersonId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MeetingAttendee>(entity =>
        {
            entity.HasOne(a => a.Meeting).WithMany(m => m.Attendees).HasForeignKey(a => a.MeetingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.DokCase).WithMany().HasForeignKey(a => a.DokCaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(a => new { a.MeetingId, a.DokCaseId }).IsUnique();
            entity.HasQueryFilter(a => a.Meeting!.DeletedAtUtc == null && a.DokCase!.DeletedAtUtc == null);
        });

        builder.Entity<Supervision>(entity =>
        {
            entity.Property(s => s.GroupLabel).IsRequired().HasMaxLength(200);
        });

        builder.Entity<Attachment>(entity =>
        {
            entity.Property(a => a.FileName).IsRequired().HasMaxLength(260);
            entity.Property(a => a.ContentType).IsRequired().HasMaxLength(150);
            entity.Property(a => a.BlobPath).IsRequired().HasMaxLength(500);
            entity.Property(a => a.UploadedByUserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(a => new { a.OwnerType, a.OwnerId });
        });

        builder.Entity<GeneratedDocument>(entity =>
        {
            entity.HasOne(d => d.Person).WithMany().HasForeignKey(d => d.PersonId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MailingCampaign>(entity =>
        {
            entity.Property(m => m.Subject).IsRequired().HasMaxLength(200);
            entity.Property(m => m.Body).IsRequired();
        });

        builder.Entity<AuditLogEntry>(entity =>
        {
            entity.Property(a => a.UserEmail).IsRequired().HasMaxLength(256);
            entity.Property(a => a.Action).IsRequired().HasMaxLength(100);
            entity.Property(a => a.ObjectDescription).IsRequired().HasMaxLength(300);
        });

        builder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(r => new { r.RoleName, r.Permission });
            entity.Property(r => r.RoleName).IsRequired().HasMaxLength(256);
            entity.Property(r => r.Permission).IsRequired().HasMaxLength(100);
        });

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)) continue;

            var method = SetSoftDeleteFilterMethod.MakeGenericMethod(entityType.ClrType);
            method.Invoke(null, new object[] { builder });
        }
    }

    private static readonly MethodInfo SetSoftDeleteFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(SetSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder builder) where TEntity : class, ISoftDeletable
    {
        builder.Entity<TEntity>().HasQueryFilter(BuildNotDeletedExpression<TEntity>());
    }

    private static Expression<Func<TEntity, bool>> BuildNotDeletedExpression<TEntity>() where TEntity : class, ISoftDeletable
        => entity => entity.DeletedAtUtc == null;
}

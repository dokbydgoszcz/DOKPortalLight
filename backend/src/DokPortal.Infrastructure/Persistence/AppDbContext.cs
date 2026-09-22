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
    public DbSet<CanonicalMission> CanonicalMissions => Set<CanonicalMission>();
    public DbSet<Formator> Formators => Set<Formator>();
    public DbSet<ParishNeed> ParishNeeds => Set<ParishNeed>();
    public DbSet<BudgetEntry> BudgetEntries => Set<BudgetEntry>();
    public DbSet<DokCase> DokCases => Set<DokCase>();
    public DbSet<CaseDocument> CaseDocuments => Set<CaseDocument>();
    public DbSet<PastoralNote> PastoralNotes => Set<PastoralNote>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<Supervision> Supervisions => Set<Supervision>();

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

        builder.Entity<CanonicalMission>(entity =>
        {
            entity.Property(m => m.ServicePlace).IsRequired().HasMaxLength(200);
            entity.Property(m => m.GrantedPlace).HasMaxLength(200);
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
            entity.HasOne(n => n.AssignedPerson).WithMany().HasForeignKey(n => n.AssignedPersonId).OnDelete(DeleteBehavior.SetNull);
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
        });

        builder.Entity<Supervision>(entity =>
        {
            entity.Property(s => s.GroupLabel).IsRequired().HasMaxLength(200);
        });
    }
}

using AccessEase.Models;
using Microsoft.EntityFrameworkCore;

namespace AccessEase.Data
{
    public class AccessEaseDbContext : DbContext
    {
        public AccessEaseDbContext(DbContextOptions<AccessEaseDbContext> options)
            : base(options) { }

        public DbSet<ScanRecord> ScanRecords { get; set; }
        public DbSet<AxeIssue> AxeIssues { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectMember> ProjectMembers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<ScanRecord>()
                .HasOne(s => s.AppUser)
                .WithMany(u => u.ScanRecords)
                .HasForeignKey(s => s.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScanRecord>()
                .HasOne(s => s.Project)
                .WithMany(p => p.ScanRecords)
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ProjectMember>()
                .HasOne(pm => pm.Project)
                .WithMany(p => p.ProjectMembers)
                .HasForeignKey(pm => pm.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectMember>()
                .HasOne(pm => pm.AppUser)
                .WithMany(u => u.ProjectMembers)
                .HasForeignKey(pm => pm.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectMember>()
                .HasIndex(pm => new { pm.ProjectId, pm.AppUserId })
                .IsUnique();

            modelBuilder.Entity<RemediationIssue>()
    .HasOne(r => r.ScanRecord)
    .WithMany()
    .HasForeignKey(r => r.ScanRecordId)
    .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BaselineApproval>()
    .HasOne(b => b.Project)
    .WithMany()
    .HasForeignKey(b => b.ProjectId)
    .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BaselineApproval>()
                .HasOne(b => b.ScanRecord)
                .WithMany()
                .HasForeignKey(b => b.ScanRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BaselineApproval>()
                .HasOne(b => b.ApprovedByUser)
                .WithMany()
                .HasForeignKey(b => b.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BaselineApproval>()
                .HasIndex(b => new { b.ProjectId, b.Url })
                .IsUnique();

            modelBuilder.Entity<ProjectUiValidationSetting>()
    .HasOne(s => s.Project)
    .WithMany()
    .HasForeignKey(s => s.ProjectId)
    .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectUiValidationSetting>()
                .HasIndex(s => s.ProjectId)
                .IsUnique();
        }

        public DbSet<RemediationIssue> RemediationIssues { get; set; }
        public DbSet<BaselineApproval> BaselineApprovals { get; set; }
        public DbSet<ProjectUiValidationSetting> ProjectUiValidationSettings { get; set; }
        public DbSet<LoginLog> LoginLogs { get; set; }
    }
}
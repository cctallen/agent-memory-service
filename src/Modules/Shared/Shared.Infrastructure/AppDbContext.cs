using AgentMemoryService.Agents.Core.Entities;
using AgentMemoryService.Memories.Core.Entities;
using AgentMemoryService.Namespaces.Core.Entities;
using AgentMemoryService.Organizations.Core.Entities;
using AgentMemoryService.Secrets.Core.Entities;
using AgentMemoryService.Users.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace AgentMemoryService.Shared.Infrastructure;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public DbSet<MemoryNamespace> Namespaces => Set<MemoryNamespace>();
    public DbSet<NamespaceMember> NamespaceMembers => Set<NamespaceMember>();
    public DbSet<Memory> Memories => Set<Memory>();
    public DbSet<MemoryAccessLog> MemoryAccessLogs => Set<MemoryAccessLog>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentNamespaceMembership> AgentNamespaces => Set<AgentNamespaceMembership>();
    public DbSet<Secret> Secrets => Set<Secret>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("vector");

        // Users
        modelBuilder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            b.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        });

        // Namespaces
        modelBuilder.Entity<MemoryNamespace>(b =>
        {
            b.ToTable("namespaces");
            b.HasKey(n => n.Id);
            b.Property(n => n.Slug).HasMaxLength(100).IsRequired();
            b.HasIndex(n => n.Slug).IsUnique();
            b.Property(n => n.DisplayName).HasMaxLength(200).IsRequired();
            b.Property(n => n.Description).HasMaxLength(1000);
            b.HasMany(n => n.Members)
                .WithOne()
                .HasForeignKey(m => m.NamespaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NamespaceMember>(b =>
        {
            b.ToTable("namespace_members");
            b.HasKey(m => new { m.NamespaceId, m.UserId });
        });

        // Memories
        modelBuilder.Entity<Memory>(b =>
        {
            b.ToTable("memories");
            b.HasKey(m => m.Id);
            b.Property(m => m.Content).HasMaxLength(10000).IsRequired();
            b.Property(m => m.Embedding).HasColumnType("vector(1536)");
            b.Property(m => m.Source).HasMaxLength(500);
            b.Property(m => m.Metadata)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());
            b.HasIndex(m => m.NamespaceId);
            b.HasIndex(m => m.AgentId);
            b.HasIndex(m => new { m.NamespaceId, m.IsActive });
            b.HasIndex(m => new { m.NamespaceId, m.Visibility, m.IsActive });
            b.Property(m => m.Visibility).HasConversion<int>();
            b.HasQueryFilter(m => m.IsActive);
        });

        // Memory Access Logs
        modelBuilder.Entity<MemoryAccessLog>(b =>
        {
            b.ToTable("memory_access_logs");
            b.HasKey(l => l.Id);
            b.Property(l => l.AgentName).HasMaxLength(200).IsRequired();
            b.Property(l => l.Query).HasMaxLength(500);
            b.Property(l => l.Operation).HasConversion<int>();
            b.HasIndex(l => l.NamespaceId);
            b.HasIndex(l => l.AgentId);
            b.HasIndex(l => l.MemoryId);
            b.HasIndex(l => l.AccessedAt);
            b.HasIndex(l => new { l.NamespaceId, l.AccessedAt });
        });

        // Agents
        modelBuilder.Entity<Agent>(b =>
        {
            b.ToTable("agents");
            b.HasKey(a => a.Id);
            b.Property(a => a.Name).HasMaxLength(200).IsRequired();
            b.Property(a => a.ApiKey).HasMaxLength(100).IsRequired();
            b.HasIndex(a => a.ApiKey).IsUnique();
            b.HasMany(a => a.Namespaces)
                .WithOne()
                .HasForeignKey(m => m.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(a => a.IsActive);
        });

        modelBuilder.Entity<AgentNamespaceMembership>(b =>
        {
            b.ToTable("agent_namespaces");
            b.HasKey(m => new { m.AgentId, m.NamespaceId });
            b.Property(m => m.Permissions)
                .HasColumnName("permissions")
                .HasDefaultValue(AgentPermissions.ReadWrite)
                .HasSentinel(AgentPermissions.None)
                .IsRequired();
        });

        // Secrets
        modelBuilder.Entity<Secret>(b =>
        {
            b.ToTable("secrets");
            b.HasKey(s => s.Id);
            b.Property(s => s.Name).HasMaxLength(200).IsRequired();
            b.Property(s => s.Description).HasMaxLength(500);
            b.Property(s => s.EncryptedValue).IsRequired();
            b.Property(s => s.Iv).HasMaxLength(100).IsRequired();
            b.HasIndex(s => new { s.NamespaceId, s.Name }).IsUnique();
            b.HasIndex(s => s.NamespaceId);
            b.HasQueryFilter(s => s.IsActive);
        });

        // Namespaces — add OrganizationId FK
        modelBuilder.Entity<MemoryNamespace>(b =>
        {
            b.Property(n => n.OrganizationId).HasColumnName("organization_id");
            b.HasIndex(n => n.OrganizationId);
        });

        // Organizations
        modelBuilder.Entity<Organization>(b =>
        {
            b.ToTable("organizations");
            b.HasKey(o => o.Id);
            b.Property(o => o.Slug).HasMaxLength(60).IsRequired();
            b.HasIndex(o => o.Slug).IsUnique();
            b.Property(o => o.DisplayName).HasMaxLength(100).IsRequired();
            b.Property(o => o.Description).HasMaxLength(500);
            b.HasMany(o => o.Members)
                .WithOne()
                .HasForeignKey(m => m.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrganizationMember>(b =>
        {
            b.ToTable("organization_members");
            b.HasKey(m => new { m.OrganizationId, m.UserId });
            b.Property(m => m.Role).IsRequired();
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<Shared.Core.IAuditable>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return await base.SaveChangesAsync(ct);
    }
}

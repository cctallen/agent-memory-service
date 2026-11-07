using AgentMemoryService.Agents.Core.Entities;
using AgentMemoryService.Memories.Core.Entities;
using AgentMemoryService.Namespaces.Core.Entities;
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
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentNamespaceMembership> AgentNamespaces => Set<AgentNamespaceMembership>();
    public DbSet<Secret> Secrets => Set<Secret>();

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

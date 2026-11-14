using AgentMemoryService.Organizations.Application.Commands;
using AgentMemoryService.Organizations.Core.Entities;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Users.Core.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Organizations.Application.Queries;

// ─── List orgs for a user ─────────────────────────────────────────────────────
public sealed record GetOrgsQuery(Guid UserId, bool IsAdmin = false) : IRequest<Result<List<OrgResponse>>>;

public sealed class GetOrgsHandler : IRequestHandler<GetOrgsQuery, Result<List<OrgResponse>>>
{
    private readonly AppDbContext _db;

    public GetOrgsHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<OrgResponse>>> Handle(GetOrgsQuery query, CancellationToken ct)
    {
        var orgs = query.IsAdmin
            ? await _db.Organizations.Include(o => o.Members).AsNoTracking().ToListAsync(ct)
            : await _db.Organizations
                .Include(o => o.Members)
                .AsNoTracking()
                .Where(o => o.OwnerId == query.UserId || o.Members.Any(m => m.UserId == query.UserId))
                .ToListAsync(ct);

        return Result<List<OrgResponse>>.Success(orgs.Select(o => o.ToResponse()).ToList());
    }
}

// ─── Get single org ───────────────────────────────────────────────────────────
public sealed record GetOrgByIdQuery(Guid OrgId, Guid RequesterId, bool IsAdmin = false) : IRequest<Result<OrgResponse>>;

public sealed class GetOrgByIdHandler : IRequestHandler<GetOrgByIdQuery, Result<OrgResponse>>
{
    private readonly AppDbContext _db;

    public GetOrgByIdHandler(AppDbContext db) => _db = db;

    public async Task<Result<OrgResponse>> Handle(GetOrgByIdQuery query, CancellationToken ct)
    {
        var org = await _db.Organizations.Include(o => o.Members)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == query.OrgId, ct);

        if (org is null)
            return Result<OrgResponse>.Failure(Error.NotFound("Org.NotFound", "Organization not found"));
        if (!query.IsAdmin && !org.HasMember(query.RequesterId))
            return Result<OrgResponse>.Failure(Error.Forbidden("Org.Forbidden", "Access denied"));

        return Result<OrgResponse>.Success(org.ToResponse());
    }
}

// ─── Get org members (with user details) ──────────────────────────────────────
public sealed record OrgMemberResponse(Guid UserId, string Email, string FullName, OrgRole Role, DateTime JoinedAt);

public sealed record GetOrgMembersQuery(Guid OrgId, Guid RequesterId, bool IsAdmin = false)
    : IRequest<Result<List<OrgMemberResponse>>>;

public sealed class GetOrgMembersHandler : IRequestHandler<GetOrgMembersQuery, Result<List<OrgMemberResponse>>>
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetOrgMembersHandler(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<Result<List<OrgMemberResponse>>> Handle(GetOrgMembersQuery query, CancellationToken ct)
    {
        var org = await _db.Organizations.Include(o => o.Members)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == query.OrgId, ct);

        if (org is null)
            return Result<List<OrgMemberResponse>>.Failure(Error.NotFound("Org.NotFound", "Organization not found"));
        if (!query.IsAdmin && !org.HasMember(query.RequesterId))
            return Result<List<OrgMemberResponse>>.Failure(Error.Forbidden("Org.Forbidden", "Access denied"));

        var memberIds = org.Members.Select(m => m.UserId).ToList();
        var users = await _userManager.Users
            .AsNoTracking()
            .Where(u => memberIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, ct);

        var result = org.Members.Select(m =>
        {
            var u = users.GetValueOrDefault(m.UserId);
            return new OrgMemberResponse(
                m.UserId,
                u?.Email ?? "",
                ((u?.FirstName ?? "") + " " + (u?.LastName ?? "")).Trim(),
                m.Role,
                m.JoinedAt);
        }).ToList();

        return Result<List<OrgMemberResponse>>.Success(result);
    }
}

// ─── Get org namespaces ───────────────────────────────────────────────────────
public sealed record GetOrgNamespacesQuery(Guid OrgId, Guid RequesterId, bool IsAdmin = false)
    : IRequest<Result<List<object>>>;

public sealed class GetOrgNamespacesHandler : IRequestHandler<GetOrgNamespacesQuery, Result<List<object>>>
{
    private readonly AppDbContext _db;

    public GetOrgNamespacesHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<object>>> Handle(GetOrgNamespacesQuery query, CancellationToken ct)
    {
        var org = await _db.Organizations.Include(o => o.Members)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == query.OrgId, ct);

        if (org is null)
            return Result<List<object>>.Failure(Error.NotFound("Org.NotFound", "Organization not found"));
        if (!query.IsAdmin && !org.HasMember(query.RequesterId))
            return Result<List<object>>.Failure(Error.Forbidden("Org.Forbidden", "Access denied"));

        var namespaces = await _db.Namespaces
            .AsNoTracking()
            .Where(n => n.OrganizationId == query.OrgId)
            .Select(n => (object)new { n.Id, n.Slug, n.DisplayName, n.Description, n.IsPublic, n.OwnerId, n.CreatedAt })
            .ToListAsync(ct);

        return Result<List<object>>.Success(namespaces);
    }
}

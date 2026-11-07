using AgentMemoryService.Secrets.Application.Commands;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Secrets.Application.Queries;

// ─────────────────────────────────────────────────────────────
// List Secrets (metadata only — no values)
// ─────────────────────────────────────────────────────────────
public sealed record GetSecretsQuery(Guid NamespaceId, Guid RequesterId, bool IsAgent = false)
    : IRequest<Result<List<SecretMetaResponse>>>;

public sealed class GetSecretsHandler : IRequestHandler<GetSecretsQuery, Result<List<SecretMetaResponse>>>
{
    private readonly AppDbContext _db;

    public GetSecretsHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<SecretMetaResponse>>> Handle(GetSecretsQuery query, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == query.NamespaceId, ct);
        if (ns is null) return Result<List<SecretMetaResponse>>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        if (!query.IsAgent && !ns.HasAccess(query.RequesterId))
            return Result<List<SecretMetaResponse>>.Failure(Error.Forbidden("Secret.Forbidden", "Read access required"));

        var secrets = await _db.Secrets
            .AsNoTracking()
            .Where(s => s.NamespaceId == query.NamespaceId)
            .OrderBy(s => s.Name)
            .Select(s => new SecretMetaResponse(s.Id, s.NamespaceId, s.Name, s.Description, s.CreatedAt, s.UpdatedAt))
            .ToListAsync(ct);

        return Result<List<SecretMetaResponse>>.Success(secrets);
    }
}

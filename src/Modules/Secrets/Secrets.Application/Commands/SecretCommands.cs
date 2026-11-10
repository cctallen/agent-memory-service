using System.Security.Cryptography;
using AgentMemoryService.Secrets.Core.Entities;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AgentMemoryService.Secrets.Application.Commands;

// ─────────────────────────────────────────────────────────────
// Shared response types
// ─────────────────────────────────────────────────────────────
/// <summary>Safe metadata-only view — never includes the plaintext value.</summary>
public sealed record SecretMetaResponse(Guid Id, Guid NamespaceId, string Name, string? Description, DateTime CreatedAt, DateTime? UpdatedAt);

/// <summary>Includes the decrypted plaintext; only returned by the reveal endpoint.</summary>
public sealed record SecretRevealResponse(Guid Id, string Name, string? Description, string Value);

public static class SecretMappings
{
    public static SecretMetaResponse ToMeta(this Secret s) =>
        new(s.Id, s.NamespaceId, s.Name, s.Description, s.CreatedAt, s.UpdatedAt);
}

// ─────────────────────────────────────────────────────────────
// Encryption helper — AES-256-GCM
// ─────────────────────────────────────────────────────────────
public sealed class SecretEncryptionService
{
    private readonly byte[] _key;

    public SecretEncryptionService(IConfiguration config)
    {
        var raw = config["Secrets:EncryptionKey"]
            ?? throw new InvalidOperationException("Secrets:EncryptionKey is not configured");
        _key = Convert.FromBase64String(raw);
        if (_key.Length != 32)
            throw new InvalidOperationException("Secrets:EncryptionKey must be a 32-byte Base64 string (AES-256)");
    }

    public (string ciphertext, string iv) Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MinSize); // 12 bytes
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];                            // 16 bytes
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plaintextBytes.Length];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintextBytes, cipherBytes, tag);

        // Store ciphertext + tag concatenated
        var combined = new byte[cipherBytes.Length + tag.Length];
        cipherBytes.CopyTo(combined, 0);
        tag.CopyTo(combined, cipherBytes.Length);

        return (Convert.ToBase64String(combined), Convert.ToBase64String(nonce));
    }

    public string Decrypt(string ciphertext, string ivBase64)
    {
        var combined = Convert.FromBase64String(ciphertext);
        var nonce = Convert.FromBase64String(ivBase64);
        var tag = combined[^AesGcm.TagByteSizes.MaxSize..];
        var cipherBytes = combined[..^AesGcm.TagByteSizes.MaxSize];
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
}

// ─────────────────────────────────────────────────────────────
// Create Secret
// ─────────────────────────────────────────────────────────────
public sealed record CreateSecretCommand(
    Guid NamespaceId,
    Guid RequesterId,
    string Name,
    string Value,
    string? Description = null,
    bool IsAgent = false)
    : IRequest<Result<SecretMetaResponse>>;

public sealed class CreateSecretHandler : IRequestHandler<CreateSecretCommand, Result<SecretMetaResponse>>
{
    private readonly AppDbContext _db;
    private readonly SecretEncryptionService _enc;

    public CreateSecretHandler(AppDbContext db, SecretEncryptionService enc)
    {
        _db = db;
        _enc = enc;
    }

    public async Task<Result<SecretMetaResponse>> Handle(CreateSecretCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);
        if (ns is null) return Result<SecretMetaResponse>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        if (!command.IsAgent && !ns.CanWrite(command.RequesterId))
            return Result<SecretMetaResponse>.Failure(Error.Forbidden("Secret.Forbidden", "Write access required"));

        // Enforce unique name per namespace
        var exists = await _db.Secrets
            .AnyAsync(s => s.NamespaceId == command.NamespaceId && s.Name == command.Name, ct);
        if (exists)
            return Result<SecretMetaResponse>.Failure(Error.Conflict("Secret.Duplicate", $"A secret named '{command.Name}' already exists in this namespace"));

        var (cipher, iv) = _enc.Encrypt(command.Value);
        var secret = Secret.Create(command.NamespaceId, command.RequesterId, command.Name, cipher, iv, command.Description);
        _db.Secrets.Add(secret);
        await _db.SaveChangesAsync(ct);

        return Result<SecretMetaResponse>.Success(secret.ToMeta());
    }
}

// ─────────────────────────────────────────────────────────────
// Update Secret
// ─────────────────────────────────────────────────────────────
public sealed record UpdateSecretCommand(
    Guid SecretId,
    Guid NamespaceId,
    Guid RequesterId,
    string Name,
    string Value,
    string? Description = null,
    bool IsAgent = false)
    : IRequest<Result<SecretMetaResponse>>;

public sealed class UpdateSecretHandler : IRequestHandler<UpdateSecretCommand, Result<SecretMetaResponse>>
{
    private readonly AppDbContext _db;
    private readonly SecretEncryptionService _enc;

    public UpdateSecretHandler(AppDbContext db, SecretEncryptionService enc)
    {
        _db = db;
        _enc = enc;
    }

    public async Task<Result<SecretMetaResponse>> Handle(UpdateSecretCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);
        if (ns is null) return Result<SecretMetaResponse>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        if (!command.IsAgent && !ns.CanWrite(command.RequesterId))
            return Result<SecretMetaResponse>.Failure(Error.Forbidden("Secret.Forbidden", "Write access required"));

        var secret = await _db.Secrets
            .FirstOrDefaultAsync(s => s.Id == command.SecretId && s.NamespaceId == command.NamespaceId, ct);
        if (secret is null) return Result<SecretMetaResponse>.Failure(Error.NotFound("Secret.NotFound", "Secret not found"));

        // Check unique name (excluding self)
        var nameConflict = await _db.Secrets
            .AnyAsync(s => s.NamespaceId == command.NamespaceId && s.Name == command.Name && s.Id != command.SecretId, ct);
        if (nameConflict)
            return Result<SecretMetaResponse>.Failure(Error.Conflict("Secret.Duplicate", $"A secret named '{command.Name}' already exists in this namespace"));

        var (cipher, iv) = _enc.Encrypt(command.Value);
        secret.Update(command.Name, command.Description, cipher, iv);
        await _db.SaveChangesAsync(ct);

        return Result<SecretMetaResponse>.Success(secret.ToMeta());
    }
}

// ─────────────────────────────────────────────────────────────
// Delete Secret
// ─────────────────────────────────────────────────────────────
public sealed record DeleteSecretCommand(Guid SecretId, Guid NamespaceId, Guid RequesterId, bool IsAgent = false)
    : IRequest<Result<bool>>;

public sealed class DeleteSecretHandler : IRequestHandler<DeleteSecretCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public DeleteSecretHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteSecretCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);
        if (ns is null) return Result<bool>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        if (!command.IsAgent && !ns.CanWrite(command.RequesterId))
            return Result<bool>.Failure(Error.Forbidden("Secret.Forbidden", "Write access required"));

        var secret = await _db.Secrets
            .FirstOrDefaultAsync(s => s.Id == command.SecretId && s.NamespaceId == command.NamespaceId, ct);
        if (secret is null) return Result<bool>.Failure(Error.NotFound("Secret.NotFound", "Secret not found"));

        secret.Deactivate();
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─────────────────────────────────────────────────────────────
// Reveal Secret by Name
// ─────────────────────────────────────────────────────────────
public sealed record RevealSecretByNameCommand(string Name, Guid NamespaceId, Guid RequesterId, bool IsAgent = false)
    : IRequest<Result<SecretRevealResponse>>;

public sealed class RevealSecretByNameCommandValidator : AbstractValidator<RevealSecretByNameCommand>
{
    public RevealSecretByNameCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class RevealSecretByNameHandler : IRequestHandler<RevealSecretByNameCommand, Result<SecretRevealResponse>>
{
    private readonly AppDbContext _db;
    private readonly SecretEncryptionService _enc;

    public RevealSecretByNameHandler(AppDbContext db, SecretEncryptionService enc)
    {
        _db = db;
        _enc = enc;
    }

    public async Task<Result<SecretRevealResponse>> Handle(RevealSecretByNameCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);
        if (ns is null) return Result<SecretRevealResponse>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));

        if (!command.IsAgent && !ns.HasAccess(command.RequesterId))
            return Result<SecretRevealResponse>.Failure(Error.Forbidden("Secret.Forbidden", "Read access required"));

        var secret = await _db.Secrets
            .FirstOrDefaultAsync(s => s.NamespaceId == command.NamespaceId && s.Name == command.Name, ct);
        if (secret is null) return Result<SecretRevealResponse>.Failure(Error.NotFound("Secret.NotFound", $"No secret named '{command.Name}' found in this namespace"));

        var plaintext = _enc.Decrypt(secret.EncryptedValue, secret.Iv);
        return Result<SecretRevealResponse>.Success(new SecretRevealResponse(secret.Id, secret.Name, secret.Description, plaintext));
    }
}
public sealed record RevealSecretCommand(Guid SecretId, Guid NamespaceId, Guid RequesterId, bool IsAgent = false)
    : IRequest<Result<SecretRevealResponse>>;

public sealed class RevealSecretHandler : IRequestHandler<RevealSecretCommand, Result<SecretRevealResponse>>
{
    private readonly AppDbContext _db;
    private readonly SecretEncryptionService _enc;

    public RevealSecretHandler(AppDbContext db, SecretEncryptionService enc)
    {
        _db = db;
        _enc = enc;
    }

    public async Task<Result<SecretRevealResponse>> Handle(RevealSecretCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);
        if (ns is null) return Result<SecretRevealResponse>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));

        // Agents and namespace members can reveal
        if (!command.IsAgent && !ns.HasAccess(command.RequesterId))
            return Result<SecretRevealResponse>.Failure(Error.Forbidden("Secret.Forbidden", "Read access required"));

        var secret = await _db.Secrets
            .FirstOrDefaultAsync(s => s.Id == command.SecretId && s.NamespaceId == command.NamespaceId, ct);
        if (secret is null) return Result<SecretRevealResponse>.Failure(Error.NotFound("Secret.NotFound", "Secret not found"));

        var plaintext = _enc.Decrypt(secret.EncryptedValue, secret.Iv);
        return Result<SecretRevealResponse>.Success(new SecretRevealResponse(secret.Id, secret.Name, secret.Description, plaintext));
    }
}

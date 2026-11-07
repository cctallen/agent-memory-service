using AgentMemoryService.Shared.Core;

namespace AgentMemoryService.Secrets.Core.Entities;

/// <summary>
/// A named secret stored encrypted at rest within a namespace.
/// Agents with access to the namespace can read the plaintext value.
/// Users see metadata only; they must explicitly call the reveal endpoint.
/// </summary>
public sealed class Secret : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid NamespaceId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    /// <summary>Encrypted ciphertext stored in DB (base64).</summary>
    public string EncryptedValue { get; private set; } = string.Empty;
    /// <summary>IV/nonce for AES-GCM (base64).</summary>
    public string Iv { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private Secret() { }

    public static Secret Create(
        Guid namespaceId,
        Guid createdByUserId,
        string name,
        string encryptedValue,
        string iv,
        string? description = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            NamespaceId = namespaceId,
            CreatedByUserId = createdByUserId,
            Name = name,
            Description = description,
            EncryptedValue = encryptedValue,
            Iv = iv,
        };

    public void Update(string name, string? description, string encryptedValue, string iv)
    {
        Name = name;
        Description = description;
        EncryptedValue = encryptedValue;
        Iv = iv;
    }

    public void Deactivate() => IsActive = false;
}

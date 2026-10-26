using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SecureNotepad.Api.Models;

/// <summary>
/// Represents an encrypted note document stored in MongoDB Atlas.
/// The server ONLY stores ciphertext and cryptographic metadata.
/// Plaintext content, user passphrases, and derived keys are NEVER stored or transmitted.
/// </summary>
public class Note
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("ciphertext")]
    public string Ciphertext { get; set; } = string.Empty;

    [BsonElement("salt")]
    public string Salt { get; set; } = string.Empty;

    [BsonElement("iv")]
    public string Iv { get; set; } = string.Empty;

    [BsonElement("encryptionVersion")]
    public int EncryptionVersion { get; set; } = 1;

    [BsonElement("kdfIterations")]
    public int KdfIterations { get; set; } = 600000;

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("expiresAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ExpiresAt { get; set; }
}


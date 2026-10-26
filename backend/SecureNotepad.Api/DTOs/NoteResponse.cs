namespace SecureNotepad.Api.DTOs;

/// <summary>
/// DTO returned when creating, retrieving, or updating an encrypted note.
/// Contains the identifier, authenticated ciphertext, and cryptographic metadata.
/// </summary>
public class NoteResponse
{
    public string Id { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string Iv { get; set; } = string.Empty;
    public int EncryptionVersion { get; set; }
    public int KdfIterations { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}


using System.ComponentModel.DataAnnotations;

namespace SecureNotepad.Api.DTOs;

/// <summary>
/// DTO for creating an encrypted note.
/// Contains only authenticated ciphertext and required cryptographic parameters.
/// </summary>
public class CreateNoteRequest
{
    [StringLength(64, MinimumLength = 6, ErrorMessage = "Id must be between 6 and 64 characters.")]
    [RegularExpression("^[a-zA-Z0-9_-]{6,64}$", ErrorMessage = "Invalid ID format.")]
    public string? Id { get; set; }

    [Required(ErrorMessage = "Ciphertext is required.")]
    [StringLength(500000, MinimumLength = 1, ErrorMessage = "Ciphertext length must be between 1 and 500,000 characters.")]
    public string Ciphertext { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cryptographic salt is required.")]
    [StringLength(128, MinimumLength = 16, ErrorMessage = "Salt must be a valid Base64 string between 16 and 128 characters.")]
    public string Salt { get; set; } = string.Empty;

    [Required(ErrorMessage = "Initialization vector (IV) is required.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "IV must be a valid Base64 string between 12 and 128 characters.")]
    public string Iv { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "Encryption version must be a supported positive integer.")]
    public int EncryptionVersion { get; set; } = 1;

    [Range(1000, 2000000, ErrorMessage = "KDF iterations must be between 1,000 and 2,000,000.")]
    public int KdfIterations { get; set; } = 600000;

    [Range(1, 8760, ErrorMessage = "Expiration in hours must be between 1 and 8760 (1 year).")]
    public int? ExpiresInHours { get; set; }
}


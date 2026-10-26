using Microsoft.AspNetCore.Mvc;
using SecureNotepad.Api.DTOs;
using SecureNotepad.Api.Models;
using SecureNotepad.Api.Services;
using System.Text.RegularExpressions;

namespace SecureNotepad.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NotesController : ControllerBase
{
    private readonly INoteRepository _repository;
    private readonly ILogger<NotesController> _logger;

    // Strict URL-safe ID format check: 8-64 alphanumeric characters
    private static readonly Regex SafeIdRegex = new("^[a-zA-Z0-9_-]{6,64}$", RegexOptions.Compiled);

    public NotesController(INoteRepository repository, ILogger<NotesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new encrypted note with client-supplied ciphertext and cryptographic parameters.
    /// The server NEVER has access to plaintext content or passphrases.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(NoteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateNote([FromBody] CreateNoteRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Use client-provided deterministic ID (lookup hash) or generate unique random ID
        var noteId = !string.IsNullOrWhiteSpace(request.Id) ? request.Id : IdGenerator.NewId(18);

        DateTime? expiresAt = null;
        if (request.ExpiresInHours.HasValue && request.ExpiresInHours.Value > 0)
        {
            expiresAt = DateTime.UtcNow.AddHours(request.ExpiresInHours.Value);
        }

        var existingNote = await _repository.GetByIdAsync(noteId, cancellationToken);
        if (existingNote != null)
        {
            existingNote.Ciphertext = request.Ciphertext;
            existingNote.Salt = request.Salt;
            existingNote.Iv = request.Iv;
            existingNote.EncryptionVersion = request.EncryptionVersion;
            existingNote.KdfIterations = request.KdfIterations;
            if (expiresAt.HasValue) existingNote.ExpiresAt = expiresAt;

            await _repository.UpdateAsync(noteId, existingNote, cancellationToken);
            _logger.LogInformation("Encrypted note updated with ID: {NoteId}", noteId);
            return Ok(ToResponse(existingNote));
        }

        var note = new Note
        {
            Id = noteId,
            Ciphertext = request.Ciphertext,
            Salt = request.Salt,
            Iv = request.Iv,
            EncryptionVersion = request.EncryptionVersion,
            KdfIterations = request.KdfIterations,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        await _repository.CreateAsync(note, cancellationToken);
        _logger.LogInformation("Encrypted note created successfully with ID: {NoteId}", noteId);

        var response = ToResponse(note);
        return CreatedAtAction(nameof(GetNote), new { id = note.Id }, response);
    }

    /// <summary>
    /// Retrieves an existing encrypted note and its cryptographic metadata by Note ID.
    /// Returns 404 if the note does not exist or has expired.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(NoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNote(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || !SafeIdRegex.IsMatch(id))
        {
            return BadRequest(new { message = "Invalid note identifier format." });
        }

        var note = await _repository.GetByIdAsync(id, cancellationToken);
        if (note == null)
        {
            return NotFound(new { message = "Note not found or has expired." });
        }

        return Ok(ToResponse(note));
    }

    /// <summary>
    /// Updates an existing encrypted note with freshly encrypted ciphertext, salt, and IV.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(NoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateNote(string id, [FromBody] UpdateNoteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || !SafeIdRegex.IsMatch(id))
        {
            return BadRequest(new { message = "Invalid note identifier format." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var existingNote = await _repository.GetByIdAsync(id, cancellationToken);
        if (existingNote == null)
        {
            var newNote = new Note
            {
                Id = id,
                Ciphertext = request.Ciphertext,
                Salt = request.Salt,
                Iv = request.Iv,
                EncryptionVersion = request.EncryptionVersion,
                KdfIterations = request.KdfIterations,
                CreatedAt = DateTime.UtcNow
            };
            await _repository.CreateAsync(newNote, cancellationToken);
            _logger.LogInformation("Encrypted note created via upsert with ID: {NoteId}", id);
            return Ok(ToResponse(newNote));
        }

        existingNote.Ciphertext = request.Ciphertext;
        existingNote.Salt = request.Salt;
        existingNote.Iv = request.Iv;
        existingNote.EncryptionVersion = request.EncryptionVersion;
        existingNote.KdfIterations = request.KdfIterations;

        var updated = await _repository.UpdateAsync(id, existingNote, cancellationToken);
        if (!updated)
        {
            return NotFound(new { message = "Note not found or update failed." });
        }

        _logger.LogInformation("Encrypted note updated successfully with ID: {NoteId}", id);
        return Ok(ToResponse(existingNote));
    }

    /// <summary>
    /// Permanently deletes an encrypted note by Note ID.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNote(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || !SafeIdRegex.IsMatch(id))
        {
            return BadRequest(new { message = "Invalid note identifier format." });
        }

        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(new { message = "Note not found or already deleted." });
        }

        _logger.LogInformation("Encrypted note deleted with ID: {NoteId}", id);
        return NoContent();
    }

    private static NoteResponse ToResponse(Note note)
    {
        return new NoteResponse
        {
            Id = note.Id,
            Ciphertext = note.Ciphertext,
            Salt = note.Salt,
            Iv = note.Iv,
            EncryptionVersion = note.EncryptionVersion,
            KdfIterations = note.KdfIterations,
            CreatedAt = note.CreatedAt,
            ExpiresAt = note.ExpiresAt
        };
    }
}


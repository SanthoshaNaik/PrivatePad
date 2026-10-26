using SecureNotepad.Api.Models;

namespace SecureNotepad.Api.Services;

public interface INoteRepository
{
    Task<Note?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Note> CreateAsync(Note note, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(string id, Note note, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}


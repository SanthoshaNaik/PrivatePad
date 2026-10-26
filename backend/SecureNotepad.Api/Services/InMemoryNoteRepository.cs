using System.Collections.Concurrent;
using SecureNotepad.Api.Models;

namespace SecureNotepad.Api.Services;

/// <summary>
/// Thread-safe in-memory repository used for automated tests and local offline development
/// when no MongoDB Atlas connection string is provided.
/// </summary>
public class InMemoryNoteRepository : INoteRepository
{
    private readonly ConcurrentDictionary<string, Note> _store = new();

    public Task<Note?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(id, out var note))
        {
            if (note.ExpiresAt.HasValue && note.ExpiresAt.Value <= DateTime.UtcNow)
            {
                _store.TryRemove(id, out _);
                return Task.FromResult<Note?>(null);
            }
            return Task.FromResult<Note?>(note);
        }

        return Task.FromResult<Note?>(null);
    }

    public Task<Note> CreateAsync(Note note, CancellationToken cancellationToken = default)
    {
        _store[note.Id] = note;
        return Task.FromResult(note);
    }

    public Task<bool> UpdateAsync(string id, Note note, CancellationToken cancellationToken = default)
    {
        _store[id] = note;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.TryRemove(id, out _));
    }

    public Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}


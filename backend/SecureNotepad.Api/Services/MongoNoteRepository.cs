using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using SecureNotepad.Api.Configuration;
using SecureNotepad.Api.Models;

namespace SecureNotepad.Api.Services;

/// <summary>
/// MongoDB Atlas implementation of INoteRepository.
/// Interacts directly with MongoDB Atlas via the official MongoDB.Driver.
/// </summary>
public class MongoNoteRepository : INoteRepository
{
    private readonly IMongoCollection<Note> _notesCollection;
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoNoteRepository> _logger;

    public MongoNoteRepository(IOptions<MongoDbSettings> settings, ILogger<MongoNoteRepository> logger)
    {
        _logger = logger;
        var config = settings.Value;

        var clientSettings = MongoClientSettings.FromConnectionString(config.ConnectionString);
        // Atlas connection tuning: set reasonable timeout for server selection
        clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);

        var client = new MongoClient(clientSettings);
        _database = client.GetDatabase(config.DatabaseName);
        _notesCollection = _database.GetCollection<Note>(config.CollectionName);

        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        try
        {
            // Note: Id is decorated with [BsonId], so MongoDB automatically creates and enforces the unique primary index on _id.
            // TTL index on ExpiresAt (automatically evicts expired notes)
            var ttlIndexKeys = Builders<Note>.IndexKeys.Ascending(n => n.ExpiresAt);
            var ttlIndexOptions = new CreateIndexOptions
            {
                ExpireAfter = TimeSpan.Zero,
                Name = "idx_note_expires_at",
                Sparse = true
            };
            _notesCollection.Indexes.CreateOne(new CreateIndexModel<Note>(ttlIndexKeys, ttlIndexOptions));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not immediately create TTL index on MongoDB Atlas. It may already exist or connection will be retried on demand.");
        }
    }

    public async Task<Note?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Note>.Filter.Eq(n => n.Id, id);
        var note = await _notesCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);

        // Check expiration in case background TTL hasn't run yet
        if (note != null && note.ExpiresAt.HasValue && note.ExpiresAt.Value <= DateTime.UtcNow)
        {
            // Note has expired, trigger background removal and return null (404)
            _ = DeleteAsync(id, cancellationToken);
            return null;
        }

        return note;
    }

    public async Task<Note> CreateAsync(Note note, CancellationToken cancellationToken = default)
    {
        await _notesCollection.InsertOneAsync(note, cancellationToken: cancellationToken);
        return note;
    }

    public async Task<bool> UpdateAsync(string id, Note note, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Note>.Filter.Eq(n => n.Id, id);
        var update = Builders<Note>.Update
            .Set(n => n.Ciphertext, note.Ciphertext)
            .Set(n => n.Salt, note.Salt)
            .Set(n => n.Iv, note.Iv)
            .Set(n => n.EncryptionVersion, note.EncryptionVersion)
            .Set(n => n.KdfIterations, note.KdfIterations)
            .SetOnInsert(n => n.Id, id)
            .SetOnInsert(n => n.CreatedAt, DateTime.UtcNow);

        var options = new UpdateOptions { IsUpsert = true };
        var result = await _notesCollection.UpdateOneAsync(filter, update, options, cancellationToken);
        return result.MatchedCount > 0 || result.UpsertedId != null;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Note>.Filter.Eq(n => n.Id, id);
        var result = await _notesCollection.DeleteOneAsync(filter, cancellationToken);
        return result.DeletedCount > 0;
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}", cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB Atlas ping failed.");
            return false;
        }
    }
}


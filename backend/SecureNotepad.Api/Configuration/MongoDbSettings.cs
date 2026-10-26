namespace SecureNotepad.Api.Configuration;

/// <summary>
/// Configuration settings for MongoDB Atlas connection.
/// </summary>
public class MongoDbSettings
{
    public const string SectionName = "MongoDbSettings";

    /// <summary>
    /// MongoDB Atlas connection string (e.g. mongodb+srv://<user>:<password>@cluster0.mongodb.net/?retryWrites=true&w=majority)
    /// If left empty or set to "InMemory", an in-memory repository will be used for testing.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Database name in MongoDB Atlas.
    /// </summary>
    public string DatabaseName { get; set; } = "PrivatePadDb";

    /// <summary>
    /// Collection name for encrypted notes.
    /// </summary>
    public string CollectionName { get; set; } = "Notes";
}


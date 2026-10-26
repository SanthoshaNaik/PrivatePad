using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SecureNotepad.Api.DTOs;
using SecureNotepad.Api.Services;
using Xunit;

namespace SecureNotepad.Api.Tests;

public class NotesApiTests : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly HttpClient _client;

    public NotesApiTests(WebApplicationFactory<global::Program> factory)
    {
        _client = factory.CreateClient();
    }

    private static CreateNoteRequest CreateValidSampleRequest() => new()
    {
        Ciphertext = "dGhpc19pc19hbl9lbmNyeXB0ZWRfY2lwaGVydGV4dA==",
        Salt = "ZGF0YXNhbHRzYWx0MTIzNA==",
        Iv = "aXYxMjM0NTY3ODkw",
        EncryptionVersion = 1,
        KdfIterations = 600000
    };

    [Fact]
    public async Task CreateNote_ValidPayload_Returns201CreatedWithEncryptedDataAndUniqueId()
    {
        var request = CreateValidSampleRequest();

        var response = await _client.PostAsJsonAsync("/api/notes", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var note = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);
        Assert.False(string.IsNullOrWhiteSpace(note.Id));
        Assert.Equal(request.Ciphertext, note.Ciphertext);
        Assert.Equal(request.Salt, note.Salt);
        Assert.Equal(request.Iv, note.Iv);
        Assert.Equal(request.EncryptionVersion, note.EncryptionVersion);
        Assert.Equal(request.KdfIterations, note.KdfIterations);
        Assert.True(note.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task GetNote_ExistingId_Returns200OkWithEncryptedPayload()
    {
        // Arrange
        var request = CreateValidSampleRequest();
        var createResponse = await _client.PostAsJsonAsync("/api/notes", request);
        var createdNote = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(createdNote);

        // Act
        var getResponse = await _client.GetAsync($"/api/notes/{createdNote.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var retrievedNote = await getResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(retrievedNote);
        Assert.Equal(createdNote.Id, retrievedNote.Id);
        Assert.Equal(createdNote.Ciphertext, retrievedNote.Ciphertext);
    }

    [Fact]
    public async Task GetNote_NonExistentId_Returns404NotFound()
    {
        var response = await _client.GetAsync("/api/notes/nonExistentNoteId123");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetNote_InvalidIdFormat_Returns400BadRequest()
    {
        // Contains illegal special character that violates safe ID regex
        var response = await _client.GetAsync("/api/notes/invalid@id!%");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateNote_ExistingId_UpdatesPayloadAndPreservesId()
    {
        // Arrange: Create note
        var request = CreateValidSampleRequest();
        var createResponse = await _client.PostAsJsonAsync("/api/notes", request);
        var createdNote = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(createdNote);

        // Act: Update note
        var updateRequest = new UpdateNoteRequest
        {
            Ciphertext = "dXBkYXRlZF9jaXBoZXJ0ZXh0X25ldw==",
            Salt = "bmV3X3NhbHRfdmFsdWVfMTI=",
            Iv = "bmV3X2l2XzEyMzQ1",
            EncryptionVersion = 1,
            KdfIterations = 600000
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/notes/{createdNote.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedNote = await updateResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(updatedNote);
        Assert.Equal(createdNote.Id, updatedNote.Id);
        Assert.Equal(updateRequest.Ciphertext, updatedNote.Ciphertext);
        Assert.Equal(updateRequest.Salt, updatedNote.Salt);
        Assert.Equal(updateRequest.Iv, updatedNote.Iv);
    }

    [Fact]
    public async Task UpdateNote_NonExistentId_UpsertsAndCreatesNote()
    {
        var updateRequest = new UpdateNoteRequest
        {
            Ciphertext = "dXBkYXRlZF9jaXBoZXJ0ZXh0",
            Salt = "c2FsdHNhbHRzYWx0MTI=",
            Iv = "aXYxMjM0NTY3ODkw",
            EncryptionVersion = 1,
            KdfIterations = 600000
        };

        var response = await _client.PutAsJsonAsync("/api/notes/nonExistentNote999", updateRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var note = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);
        Assert.Equal("nonExistentNote999", note.Id);
    }

    [Fact]
    public async Task CreateNote_WithCustomLookupId_PersistsWithProvidedId()
    {
        var request = CreateValidSampleRequest();
        request.Id = "customSecretHash1234567890abcdef";

        var response = await _client.PostAsJsonAsync("/api/notes", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var note = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(note);
        Assert.Equal("customSecretHash1234567890abcdef", note.Id);
    }

    [Fact]
    public async Task DeleteNote_ExistingId_DeletesNoteAndSubsequentGetReturns404()
    {
        // Arrange
        var request = CreateValidSampleRequest();
        var createResponse = await _client.PostAsJsonAsync("/api/notes", request);
        var createdNote = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(createdNote);

        // Act: Delete
        var deleteResponse = await _client.DeleteAsync($"/api/notes/{createdNote.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify it is gone
        var getAfterDelete = await _client.GetAsync($"/api/notes/{createdNote.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
    }

    [Fact]
    public async Task DeleteNote_NonExistentId_Returns404NotFound()
    {
        var response = await _client.DeleteAsync("/api/notes/nonExistentNote404");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_EmptyCiphertext_Returns400BadRequest()
    {
        var request = new CreateNoteRequest
        {
            Ciphertext = "",
            Salt = "ZGF0YXNhbHRzYWx0MTIzNA==",
            Iv = "aXYxMjM0NTY3ODkw",
            EncryptionVersion = 1,
            KdfIterations = 600000
        };

        var response = await _client.PostAsJsonAsync("/api/notes", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_WeakKdfIterations_Returns400BadRequest()
    {
        var request = new CreateNoteRequest
        {
            Ciphertext = "dGVzdF9jaXBoZXI=",
            Salt = "ZGF0YXNhbHRzYWx0MTIzNA==",
            Iv = "aXYxMjM0NTY3ODkw",
            EncryptionVersion = 1,
            KdfIterations = 50 // Too low, below minimum 1000
        };

        var response = await _client.PostAsJsonAsync("/api/notes", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredNote_Returns404NotFound()
    {
        var request = CreateValidSampleRequest();
        var createResponse = await _client.PostAsJsonAsync("/api/notes", request);
        var createdNote = await createResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(createdNote);

        // Let's create an expired note directly through repository test or verify TTL behavior
        var note = new Models.Note
        {
            Id = "expiredNote123",
            Ciphertext = "dGVzdF9jaXBoZXI=",
            Salt = "ZGF0YXNhbHRzYWx0MTIzNA==",
            Iv = "aXYxMjM0NTY3ODkw",
            EncryptionVersion = 1,
            KdfIterations = 600000,
            CreatedAt = DateTime.UtcNow.AddHours(-5),
            ExpiresAt = DateTime.UtcNow.AddHours(-1) // Expired 1 hour ago
        };

        var repo = new InMemoryNoteRepository();
        await repo.CreateAsync(note);
        var retrieved = await repo.GetByIdAsync("expiredNote123");
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task HealthEndpoint_Returns200OkWithStatus()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
        Assert.Contains("PrivatePad API", content);
    }

    [Fact]
    public async Task Cors_OptionsPreflight_ReturnsAllowHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/notes");
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}

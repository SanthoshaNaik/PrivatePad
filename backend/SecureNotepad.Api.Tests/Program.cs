using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureNotepad.Api.Tests;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Running backend integration and API tests directly...");
        using var factory = new WebApplicationFactory<global::Program>();
        var tests = new NotesApiTests(factory);

        var methods = new (string Name, Func<Task> Action)[]
        {
            ("CreateNote_ValidPayload_Returns201CreatedWithEncryptedDataAndUniqueId", tests.CreateNote_ValidPayload_Returns201CreatedWithEncryptedDataAndUniqueId),
            ("GetNote_ExistingId_Returns200OkWithEncryptedPayload", tests.GetNote_ExistingId_Returns200OkWithEncryptedPayload),
            ("GetNote_NonExistentId_Returns404NotFound", tests.GetNote_NonExistentId_Returns404NotFound),
            ("GetNote_InvalidIdFormat_Returns400BadRequest", tests.GetNote_InvalidIdFormat_Returns400BadRequest),
            ("UpdateNote_ExistingId_UpdatesPayloadAndPreservesId", tests.UpdateNote_ExistingId_UpdatesPayloadAndPreservesId),
            ("UpdateNote_NonExistentId_UpsertsAndCreatesNote", tests.UpdateNote_NonExistentId_UpsertsAndCreatesNote),
            ("CreateNote_WithCustomLookupId_PersistsWithProvidedId", tests.CreateNote_WithCustomLookupId_PersistsWithProvidedId),
            ("DeleteNote_ExistingId_DeletesNoteAndSubsequentGetReturns404", tests.DeleteNote_ExistingId_DeletesNoteAndSubsequentGetReturns404),
            ("DeleteNote_NonExistentId_Returns404NotFound", tests.DeleteNote_NonExistentId_Returns404NotFound),
            ("CreateNote_EmptyCiphertext_Returns400BadRequest", tests.CreateNote_EmptyCiphertext_Returns400BadRequest),
            ("CreateNote_WeakKdfIterations_Returns400BadRequest", tests.CreateNote_WeakKdfIterations_Returns400BadRequest),
            ("ExpiredNote_Returns404NotFound", tests.ExpiredNote_Returns404NotFound),
            ("HealthEndpoint_Returns200OkWithStatus", tests.HealthEndpoint_Returns200OkWithStatus),
            ("Cors_OptionsPreflight_ReturnsAllowHeaders", tests.Cors_OptionsPreflight_ReturnsAllowHeaders)
        };

        int passed = 0;
        int failed = 0;

        foreach (var (name, action) in methods)
        {
            try
            {
                await action();
                Console.WriteLine($"[PASS] {name}");
                passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {name}: {ex.Message}");
                Console.ResetColor();
                failed++;
            }
        }

        Console.WriteLine($"\nBackend Integration Tests Complete: {passed} passed, {failed} failed.");
        return failed == 0 ? 0 : 1;
    }
}


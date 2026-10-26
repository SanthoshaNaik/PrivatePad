using System.Security.Cryptography;

namespace SecureNotepad.Api.Services;

/// <summary>
/// Generates cryptographically secure, high-entropy, URL-safe unique note identifiers.
/// </summary>
public static class IdGenerator
{
    private static readonly char[] Characters =
        "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    public static string NewId(int length = 16)
    {
        var result = new char[length];
        var bytes = new byte[length];

        RandomNumberGenerator.Fill(bytes);

        for (int i = 0; i < length; i++)
        {
            result[i] = Characters[bytes[i] % Characters.Length];
        }

        return new string(result);
    }
}


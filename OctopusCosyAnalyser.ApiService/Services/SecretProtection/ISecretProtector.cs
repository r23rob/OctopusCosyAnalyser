namespace OctopusCosyAnalyser.ApiService.Services.SecretProtection;

/// <summary>
/// Protects secrets (API keys, passwords) at rest using ASP.NET Core Data Protection.
/// Implementations are thread-safe; resolved as a singleton.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts a plaintext secret. Empty / null inputs round-trip unchanged.</summary>
    string? Protect(string? plaintext);

    /// <summary>
    /// Decrypts ciphertext written by Protect(). If the input is not valid ciphertext
    /// (e.g. legacy plaintext written before encryption was wired up), the value is
    /// returned as-is so reads from older rows don't break.
    /// </summary>
    string? Unprotect(string? ciphertext);
}

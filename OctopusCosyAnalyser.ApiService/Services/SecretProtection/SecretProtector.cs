using Microsoft.AspNetCore.DataProtection;

namespace OctopusCosyAnalyser.ApiService.Services.SecretProtection;

public sealed class SecretProtector : ISecretProtector
{
    // Single purpose string for all secret-at-rest payloads. Don't change after deploy —
    // existing ciphertext is bound to this purpose.
    private const string Purpose = "OctopusCosyAnalyser.SecretAtRest.v1";

    private readonly IDataProtector _protector;

    public SecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string? Protect(string? plaintext)
        => string.IsNullOrEmpty(plaintext) ? plaintext : _protector.Protect(plaintext);

    public string? Unprotect(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return ciphertext;
        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch
        {
            // Legacy plaintext (written before encryption was enabled) round-trips as-is.
            // On next save, the value converter will encrypt it.
            return ciphertext;
        }
    }
}

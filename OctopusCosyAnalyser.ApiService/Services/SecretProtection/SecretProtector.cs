using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace OctopusCosyAnalyser.ApiService.Services.SecretProtection;

public sealed class SecretProtector : ISecretProtector
{
    // Single purpose string for all secret-at-rest payloads. Don't change after deploy —
    // existing ciphertext is bound to this purpose.
    private const string Purpose = "OctopusCosyAnalyser.SecretAtRest.v1";

    private readonly IDataProtector _protector;
    private readonly ILogger<SecretProtector> _logger;

    public SecretProtector(IDataProtectionProvider provider, ILogger<SecretProtector> logger)
    {
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
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
        catch (CryptographicException ex)
        {
            // The Data Protection API throws CryptographicException for: tampered payloads,
            // unknown key id (rotated/lost key), and — most importantly here — values that
            // were written before encryption was wired up (legacy plaintext). We can't tell
            // which case is which from the exception alone, so log at debug and pass the
            // value through unchanged. The next save will re-encrypt legitimate plaintext;
            // tampered/lost-key payloads will surface as wrong-looking secrets downstream.
            _logger.LogDebug(ex, "SecretProtector.Unprotect failed; treating value as legacy plaintext");
            return ciphertext;
        }
    }
}

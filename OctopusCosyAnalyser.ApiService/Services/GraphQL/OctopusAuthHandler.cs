namespace OctopusCosyAnalyser.ApiService.Services.GraphQL;

/// <summary>
/// DelegatingHandler that adds the Octopus Energy auth token to each request.
/// Reads credentials from the ambient OctopusRequestContext.
/// The token is added as-is to the Authorization header (no Bearer/JWT prefix).
/// </summary>
public class OctopusAuthHandler : DelegatingHandler
{
    private readonly IOctopusTokenService _tokenService;

    public OctopusAuthHandler(IOctopusTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var settings = OctopusRequestContext.Current
            ?? throw new InvalidOperationException(
                "OctopusRequestContext.Current must be set before making GraphQL queries. " +
                "Call OctopusRequestContext.Current = settings before using the ZeroQL client.");

        var token = await _tokenService.GetAuthTokenAsync(settings, cancellationToken);
        request.Headers.Remove("Authorization");
        request.Headers.TryAddWithoutValidation("Authorization", token);

        return await base.SendAsync(request, cancellationToken);
    }
}

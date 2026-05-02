namespace OctopusCosyAnalyser.ApiService.Services.GraphQL;

/// <summary>
/// DelegatingHandler that adds the Octopus Energy auth token to each request.
/// Reads credentials from the ambient OctopusRequestContext.
/// The token is added as-is to the Authorization header (no Bearer/JWT prefix).
/// Handles 403 responses by evicting the token and retrying once.
/// </summary>
public class OctopusAuthHandler : DelegatingHandler
{
    private readonly IOctopusTokenService _tokenService;
    private readonly ILogger<OctopusAuthHandler> _logger;

    public OctopusAuthHandler(IOctopusTokenService tokenService, ILogger<OctopusAuthHandler> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var settings = OctopusRequestContext.Current
            ?? throw new InvalidOperationException(
                "OctopusRequestContext.Current must be set before making GraphQL queries. " +
                "Call OctopusRequestContext.Current = settings before using the ZeroQL client.");

        // First attempt
        var token = await _tokenService.GetAuthTokenAsync(settings, cancellationToken);
        request.Headers.Remove("Authorization");
        request.Headers.TryAddWithoutValidation("Authorization", token);

        var response = await base.SendAsync(request, cancellationToken);

        // If we get a 403, evict the token and retry once
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("Received 403 from Octopus API, evicting token and retrying once");

            _tokenService.EvictToken(settings);

            // Clone the request for retry (can't reuse the original)
            using var retryRequest = new HttpRequestMessage(request.Method, request.RequestUri);
            if (request.Content != null)
            {
                var content = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                retryRequest.Content = new ByteArrayContent(content);
                foreach (var header in request.Content.Headers)
                {
                    retryRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            foreach (var header in request.Headers)
            {
                retryRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Get a fresh token and retry
            var newToken = await _tokenService.GetAuthTokenAsync(settings, cancellationToken);
            retryRequest.Headers.Remove("Authorization");
            retryRequest.Headers.TryAddWithoutValidation("Authorization", newToken);

            response.Dispose();
            response = await base.SendAsync(retryRequest, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Still got 403 after retry. Response: {Response}", responseBody);
            }
        }

        return response;
    }
}

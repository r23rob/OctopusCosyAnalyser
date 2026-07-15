var builder = DistributedApplication.CreateBuilder(args);

// The https launch profile listens on https://localhost:7558 with the dev cert
// (trusted via `dotnet dev-certs https --trust`) terminating TLS. CI runners don't
// trust that cert, so the health check fails with UntrustedRoot there — the CI
// workflow sets APPHOST_API_LAUNCH_PROFILE=http to run everything over plain http.
var apiLaunchProfile = builder.Configuration["APPHOST_API_LAUNCH_PROFILE"] ?? "https";
var apiService = builder.AddProject<Projects.OctopusCosyAnalyser_ApiService>(
        "apiservice",
        launchProfileName: apiLaunchProfile)
    .WithHttpHealthCheck("/health", endpointName: apiLaunchProfile);

// React + Vite frontend (replaces the Blazor Web project)
// In dev: runs `npm run dev` and injects the API URL via VITE_API_TARGET.
// In publish: builds the Dockerfile in octopus-cosy-web/ for the production image.
builder.AddViteApp("webfrontend", "../octopus-cosy-web")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment("VITE_API_TARGET", apiService.GetEndpoint(apiLaunchProfile))
    // isProxied: false skips Aspire's reverse-proxy so the dashboard links directly
    // to Vite on 5173 — avoids a second random port and the auth-cookie/origin
    // confusion that comes from running the SPA on two URLs at once.
    .WithHttpEndpoint(env: "VITE_PORT", port: 5173, name: "vite", isProxied: false)
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();

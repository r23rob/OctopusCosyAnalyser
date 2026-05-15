var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL database with persistent volume
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume("postgres-data");  // Persist database files to a Docker volume

var cosydb = postgres.AddDatabase("cosydb");

// launchProfileName: "https" picks the https launch profile from the API's
// launchSettings.json — the API listens on https://localhost:7558 and the
// dev cert (trusted via `dotnet dev-certs https --trust`) terminates TLS.
var apiService = builder.AddProject<Projects.OctopusCosyAnalyser_ApiService>(
        "apiservice",
        launchProfileName: "https")
    .WithReference(cosydb)
    .WaitFor(cosydb)
    .WithHttpHealthCheck("/health", endpointName: "https");

// React + Vite frontend (replaces the Blazor Web project)
// In dev: runs `npm run dev` and injects the API URL via VITE_API_TARGET.
// In publish: builds the Dockerfile in octopus-cosy-web/ for the production image.
builder.AddViteApp("webfrontend", "../octopus-cosy-web")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment("VITE_API_TARGET", apiService.GetEndpoint("https"))
    // isProxied: false skips Aspire's reverse-proxy so the dashboard links directly
    // to Vite on 5173 — avoids a second random port and the auth-cookie/origin
    // confusion that comes from running the SPA on two URLs at once.
    .WithHttpEndpoint(env: "VITE_PORT", port: 5173, name: "vite", isProxied: false)
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();

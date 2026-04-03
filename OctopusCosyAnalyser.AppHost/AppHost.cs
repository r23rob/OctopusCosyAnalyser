var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL database with persistent volume
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume("postgres-data");  // Persist database files to a Docker volume

var cosydb = postgres.AddDatabase("cosydb");

var apiService = builder.AddProject<Projects.OctopusCosyAnalyser_ApiService>("apiservice")
    .WithReference(cosydb)
    .WaitFor(cosydb)
    .WithHttpHealthCheck("/health");

// React + Vite frontend (replaces the Blazor Web project)
// In dev: runs `npm run dev` and injects the API URL via VITE_API_TARGET
// In publish: builds the Dockerfile in octopus-cosy-web/ for the production image
builder.AddViteApp("webfrontend", "../octopus-cosy-web")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment("VITE_API_TARGET", apiService.GetEndpoint("http"))
    .WithHttpEndpoint(env: "VITE_PORT", port: 5173, name: "vite")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();

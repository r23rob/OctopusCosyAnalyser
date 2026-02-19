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

builder.AddProject<Projects.OctopusCosyAnalyser_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("env");

// TODO: use configuration for the ports and other settings
// cause hardcoding doesnt work for production scenarios
var postgres = builder.AddPostgres("postgres");
    // Name the endpoint to avoid default 'tcp' conflict
    // Host port 5433 -> container port 5432 to avoid conflicts and ensure mapping is created
    //.WithEndpoint(name: "postgres", port: 5433, targetPort: 5432);
var backtestDb = postgres.AddDatabase("backtestdb");

var cache = builder.AddRedis("cache");

var backtestApi = builder.AddProject<Projects.AspireApp1_BacktestApi>("backtestapi")
    .WithReference(cache)
    .WithReference(backtestDb)
    .WaitFor(cache)
    .WaitFor(backtestDb)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var backtestWorker = builder.AddProject<Projects.AspireApp1_BacktestWorker>("backtestworker")
    .WithReference(cache)
    .WithReference(backtestDb)
    .WaitFor(cache)
    .WaitFor(backtestDb);

var server = builder.AddProject<Projects.AspireApp1_Server>("server")
    .WithReference(cache)
    .WaitFor(cache)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(server)
    .WaitFor(server)
    .WithReference(backtestApi)
    .WaitFor(backtestApi);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();

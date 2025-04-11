// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add AspNet token validation
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

// Add AgentApplicationOptions from appsettings config.
builder.AddAgentApplicationOptions();

// Add the Agent
builder.AddAgent<AuthAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRouting();
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
    {
        await adapter.ProcessAsync(request, response, agent, cancellationToken);
    })
    .AllowAnonymous();


// Setup development host and allow use of a local launchSettings.json file
if (app.Environment.IsDevelopment())
{
    string launchSettingsPath = Path.Combine(app.Environment.ContentRootPath, "Properties", "launchSettings.json");
    if (!File.Exists(launchSettingsPath))
    {
        // No local launch settings.. use default port
        // Setup port and listening address.
        app.Urls.Add("http://localhost:3978");
    }
    else
    {
        app.MapGet("/", () => "Microsoft Agents SDK Sample");
    }
}
app.Run();


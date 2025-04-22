using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.Agents.Storage;
using TeamsAgent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Logging.AddConsole();

builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

builder.AddAgentApplicationOptions();

builder.AddAgent<MyAgent>();

builder.Services.AddSingleton<IStorage, MemoryStorage>();

var app = builder.Build();

app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});
app.Run();
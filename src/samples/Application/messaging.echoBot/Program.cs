using EchoBot;
using EchoBot.Model;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Teams.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Logging.AddConsole();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Create the storage to persist turn state
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add Connections object to access configured token connections.
builder.Services.AddSingleton<IConnections, ConfigurationConnections>();

// Add factory for ConnectorClient and UserTokenClient creation
builder.Services.AddSingleton<IChannelServiceClientFactory, RestChannelServiceClientFactory>();

builder.Services.AddCloudAdapter<CloudAdapter>();

// Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
builder.Services.AddTransient<IBot>(sp =>
{
    IStorage storage = sp.GetService<IStorage>();
    ApplicationOptions<AppState> applicationOptions = new()
    {
        Storage = storage,
        TurnStateFactory = () =>
        {
            return new AppState();
        }
    };

    return new EchoBotApplication(applicationOptions);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "Microsoft Copilot SDK Sample");
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}
app.Run();


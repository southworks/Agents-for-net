// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.Setup;
using Microsoft.Agents.Samples.Bots;
using Microsoft.Agents.State;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Add Connections object to access configured token connections.
builder.Services.AddSingleton<IConnections, ConfigurationConnections>();

// Add factory for ConnectorClient and UserTokenClient creation
builder.Services.AddSingleton<IChannelServiceClientFactory, RestChannelServiceClientFactory>();

// Create the storage we'll be using for User and Conversation state. (Memory is great for testing purposes.)
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Create the Conversation state. (Used by the Dialog system itself.)
builder.Services.AddSingleton<ConversationState>();

// Add the BotAdapter, this is the default adapter that works with Azure Bot Service and Activity Protocol.
builder.Services.AddCloudAdapter();

builder.AddBot<ActivityBot>();

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


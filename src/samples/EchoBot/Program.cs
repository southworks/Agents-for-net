// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using EchoBot;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Logging.AddConsole();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Add ApplicationOptions
builder.Services.AddTransient(sp =>
{
    return new AgentApplicationOptions()
    {
        StartTypingTimer = false,
        TurnStateFactory = () => new TurnState(sp.GetService<IStorage>())
    };
});

builder.Services.AddHeaderPropagation(options =>
{
    // Propagate if the header exists
    options.Headers.Add("x-ms-correlation-id");
});

builder.Services.AddHttpClient("MyClient", c => c.DefaultRequestHeaders.Add("x-ms-correlation-id", "111")).AddHeaderPropagation();

// Add the bot (which is transient)
builder.AddBot<MyBot>();


var app = builder.Build();

app.UseHeaderPropagation();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "Microsoft Agents SDK Sample");
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}
app.Run();


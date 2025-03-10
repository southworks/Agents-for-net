// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using EchoBot;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddHttpClient();
builder.Services.AddHttpClient("MyClient").AddHeaderPropagation(o => o.Headers.Add("X-Ms-Correlation-Id"));

builder.Services.AddHeaderPropagation(options =>
{
    // Propagate if the header exists
    options.Headers.Add("X-Ms-Correlation-Id");
});

builder.Logging.AddConsole();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Add AgentApplicationOptions.  This will use DI'd services and IConfiguration for construction.
builder.Services.AddTransient<AgentApplicationOptions>();

builder.Services.AddControllers();

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


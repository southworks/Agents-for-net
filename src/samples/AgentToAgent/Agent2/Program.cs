// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Agent2;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Add AgentApplicationOptions.  This will use DI'd services and IConfiguration for construction.
builder.Services.AddTransient<AgentApplicationOptions>();

// Add basic Agent functionality
builder.AddBot<Echo>();

var app = builder.Build();

// Required for providing the Agent manifest.
app.UseHttpsRedirection();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "Microsoft Agents SDK Sample - AgentToAgent Sample - Agent2");
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}

app.Run();


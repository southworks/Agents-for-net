// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using WeatherBot;
using Microsoft.Agents.Hosting.Setup;
using Microsoft.Agents.Protocols.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using WeatherBot.Agents;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Register Semantic Kernel
builder.Services.AddKernel();

// Register the AI service of your choice. AzureOpenAI and OpenAI are demonstrated...
if (builder.Configuration.GetSection("AIServices").GetValue<bool>("UseAzureOpenAI"))
{
    builder.Services.AddAzureOpenAIChatCompletion(
        deploymentName: builder.Configuration.GetSection("AIServices:AzureOpenAI").GetValue<string>("DeploymentName"),
        endpoint: builder.Configuration.GetSection("AIServices:AzureOpenAI").GetValue<string>("Endpoint"),
        apiKey: builder.Configuration.GetSection("AIServices:AzureOpenAI").GetValue<string>("ApiKey"));
}
else
{
    builder.Services.AddOpenAIChatCompletion(
        modelId: builder.Configuration.GetSection("AIServices:OpenAI").GetValue<string>("ModelId"),
        apiKey: builder.Configuration.GetSection("AIServices:OpenAI").GetValue<string>("ApiKey"));
}

// Register the WeatherForecastAgent
builder.Services.AddTransient<WeatherForecastAgent>();

builder.AddBot<IBot, MyBot>();

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


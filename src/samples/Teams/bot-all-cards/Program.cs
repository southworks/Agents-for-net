﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using BotAllCards.Bots;
using BotAllCards.Dialogs;
using Microsoft.Agents.State;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
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

// Add basic bot functionality
builder.AddBot<TeamsBot>();

// Create the storage we'll be using for User and Conversation state. (Memory is great for testing purposes.)
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Create the User state. (Used in this bot's Dialog implementation.)
builder.Services.AddSingleton<UserState>();

// Create the Conversation state. (Used by the Dialog system itself.)
builder.Services.AddTransient<ConversationState>();

// The Dialog that will be run by the bot.
builder.Services.AddTransient<MainDialog>();

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

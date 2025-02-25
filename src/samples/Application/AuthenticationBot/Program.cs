// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AuthenticationBot;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.App.UserAuth;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.BotBuilder.UserAuth.TokenService;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.Agents.Storage;
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

// Create the bot as a transient.
builder.Services.AddTransient<ITurnState>(sp => new TurnState(sp.GetService<IStorage>()));
builder.AddBot((sp) =>
{
    var adapter = sp.GetService<IChannelAdapter>();
    var storage = sp.GetService<IStorage>();

    var authOptions = new UserAuthenticationOptions()
    {
        // Auto-SignIn will use this OAuth flow
        Default = "graph",

        AutoSignIn = UserAuthenticationOptions.AutoSignInOn, //UserAuthenticationOptions.AutoSignInOff,

        Handlers =
        [
            new OAuthAuthentication(
                "graph",
                new OAuthSettings()
                {
                    ConnectionName = builder.Configuration["ConnectionName"]
                },
                storage)]
    };

    var appOptions = new ApplicationOptions()
    {
        Adapter = adapter,
        StartTypingTimer = true,
        UserAuthentication = authOptions,
        TurnStateFactory = () => sp.GetService<ITurnState>()
    };

    var app = new Application(appOptions);

    app.Authentication.OnUserSignInSuccess(async (turnContext, turnState, flowName, tokenResponse, cancellationToken) =>
    {
        await turnContext.SendActivityAsync($"Successfully logged in to '{flowName}'", cancellationToken: cancellationToken);
        await turnContext.SendActivityAsync($"Token string length: {tokenResponse.Token.Length}", cancellationToken: cancellationToken);
    });

    app.Authentication.OnUserSignInFailure(async (turnContext, turnState, flowName, response, cancellationToken) =>
    {
        // Failed to login
        await turnContext.SendActivityAsync($"Failed to login to '{flowName}'", cancellationToken: cancellationToken);
        await turnContext.SendActivityAsync($"Error message: {response.Error.Message}", cancellationToken: cancellationToken);
    });

    app.OnMessage("/signin", async (turnContext, turnState, cancellationToken) =>
    {
        await app.Authentication.GetTokenOrStartSignInAsync(turnContext, turnState, "graph", cancellationToken);
    });

    // Listen for user to say "/reset" and then delete state
    app.OnMessage("/reset", async (turnContext, turnState, cancellationToken) =>
    {
        await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
        await turnState.User.DeleteStateAsync(turnContext, cancellationToken);
        await turnContext.SendActivityAsync("Ok I've deleted the current turn state", cancellationToken: cancellationToken);
    });

    // Listen for user to say "/signout" and then delete cached token
    app.OnMessage("/signout", async (turnContext, turnState, cancellationToken) =>
    {
        await app.Authentication.SignOutUserAsync(turnContext, turnState, cancellationToken: cancellationToken);
        await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
    });

    // Display a welcome message
    app.OnConversationUpdate(ConversationUpdateEvents.MembersAdded, async (turnContext, turnState, cancellationToken) =>
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Type anything to start sign in."), cancellationToken);
            }
        }
    });

    // Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
    app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
    {
        int count = turnState.Conversation.IncrementMessageCount();

        await turnContext.SendActivityAsync($"[{count}] you said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    });

    return app;
});

var app = builder.Build();

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


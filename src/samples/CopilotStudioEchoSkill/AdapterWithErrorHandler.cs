﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Builder;
using System.Threading;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;

namespace CopilotStudioEchoSkill;

public class AdapterWithErrorHandler : CloudAdapter
{
    private readonly ILogger _logger;

    public AdapterWithErrorHandler(IChannelServiceClientFactory channelServiceClientFactory, IActivityTaskQueue activityTaskQueue, ILogger<IAgentHttpAdapter> logger)
                : base(channelServiceClientFactory, activityTaskQueue, logger: logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Replace the default error handler
        OnTurnError = HandleTurnError;
    }

    private async Task HandleTurnError(ITurnContext turnContext, Exception exception)
    {
        // Log any leaked exception from the application.
        _logger.LogError(exception, "[OnTurnError] unhandled error : {ExceptionMessage}", exception.Message);

        await SendErrorMessageAsync(turnContext, exception);
        await SendEoCToParentAsync(turnContext, exception);
    }

    private async Task SendErrorMessageAsync(ITurnContext turnContext, Exception exception)
    {
        try
        {
            // Send a message to the user.
            var errorMessageText = "The skill encountered an error or bug.";
            var errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.IgnoringInput.ToString());
            await turnContext.SendActivityAsync(errorMessage, CancellationToken.None);

            errorMessageText = "To continue to run this bot, please fix the bot source code.";
            errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.ExpectingInput.ToString());
            await turnContext.SendActivityAsync(errorMessage, CancellationToken.None);

            // Send a trace activity
            await turnContext.TraceActivityAsync("OnTurnError Trace", exception.ToString(), "https://www.botframework.com/schemas/error", "TurnError");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception caught in SendErrorMessageAsync : {ExceptionMessage}", ex.Message);
        }
    }

    private async Task SendEoCToParentAsync(ITurnContext turnContext, Exception exception)
    {
        try
        {
            // Send an EndOfConversation activity to the caller with the error to end the conversation,
            // and let the caller decide what to do.
            var endOfConversation = Activity.CreateEndOfConversationActivity();
            endOfConversation.Code = "SkillError";
            endOfConversation.Text = exception.Message;
            await turnContext.SendActivityAsync(endOfConversation, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception caught in SendEoCToParentAsync: {ExceptionMessage}", ex.Message);
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.State;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.BotBuilder.App.BotResponses
{
    public delegate Task BotResponseHandler(ITurnContext turnContext, ITurnState turnState, BotConversationReference reference, IActivity botActivity, CancellationToken cancellationToken);

    public class BotResponsesFeature
    {
        private readonly AgentApplication _app;

        public BotResponsesFeature(AgentApplication app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        public void OnBotResponse(BotResponseHandler handler, ushort rank = RouteRank.First)
        {
            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                var botResponse = ProtocolJsonSerializer.ToObject<AdapterBotResponseHandler.BotResponse>(turnContext.Activity.Value);
                await handler(turnContext, turnState, botResponse.BotConversationReference, botResponse.Activity, cancellationToken).ConfigureAwait(false);
            };

            _app.OnActivity(
                (turnContext, CancellationToken) =>
                    Task.FromResult(string.Equals(ActivityTypes.Event, turnContext.Activity.Type, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(AdapterBotResponseHandler.BotResponseEventName, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase)),
                routeHandler,
                rank);
        }
    }
}

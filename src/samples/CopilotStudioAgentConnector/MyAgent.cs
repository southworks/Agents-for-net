// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace CopilotStudioAgentConnector
{
    public class MyAgent : AgentApplication
    {
        public MyAgent(AgentApplicationOptions options) : base(options)
        {
            // Handle Messages from MCS with auth
            OnActivity((tc,ct) => Task.FromResult(tc.Activity.IsType(ActivityTypes.Message) && tc.Activity.Recipient.Role == RoleTypes.ConnectorUser), OnMCSMessage);
        }

        private async Task OnMCSMessage(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            var name = await GetDisplayName(turnContext);
            await turnContext.SendActivityAsync($"Hi, {name}", cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Gets the display name of the user from the Graph API using the access token.
        /// </summary>
        private async Task<string> GetDisplayName(ITurnContext turnContext)
        {
            string displayName = "Unknown";
            var graphInfo = await GetGraphInfo(turnContext);
            if (graphInfo != null)
            {
                displayName = graphInfo!["displayName"]!.GetValue<string>();
            }
            return displayName;
        }

        private async Task<JsonNode> GetGraphInfo(ITurnContext turnContext)
        {
            string accessToken = await UserAuthorization.GetTurnTokenAsync(turnContext);
            string graphApiUrl = $"https://graph.microsoft.com/v1.0/me";
            try
            {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                HttpResponseMessage response = await client.GetAsync(graphApiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonNode.Parse(content)!;
                }
            }
            catch (Exception ex)
            {
                // Handle error response from Graph API
                System.Diagnostics.Trace.WriteLine($"Error getting display name: {ex.Message}");
            }
            return null!;
        }
    }
}

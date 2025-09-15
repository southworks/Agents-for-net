// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    public class AgenticAuthorization
    {
        /*
        Demo Activity.Recipient

        "recipient":
        {
          "id":"34bde265-6abe-4392-9f2a-90063f156f4a", // AA
          "name":"saapp1user1@projectkairoentra.onmicrosoft.com", //AU UPN
          "aadObjectId":"cc8beb3e-8e7a-4f33-91da-08c612099a58", // AU Oid
          "aadClientId":"52fb5abc-26cb-4ede-b26c-0aa4c1f2154c", // AAI
          "role":"agentuser"
        } 
        */

        private readonly IConnections _connections;

        public static bool IsAgenticRequest(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            return IsAgenticRequest(turnContext.Activity);
        }

        public static bool IsAgenticRequest(IActivity activity)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            return activity?.Recipient?.Role == RoleTypes.AgentIdentity
                || activity?.Recipient?.Role == RoleTypes.AgentUser;
        }

        public static string GetAgentInstanceId(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));

            if (!IsAgenticRequest(turnContext)) return null;
            return turnContext?.Activity?.Recipient?.AadClientId;
        }

        public static string GetAgentUser(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));

            if (!IsAgenticRequest(turnContext)) return null;
            return turnContext?.Activity?.Recipient?.Name;  // What the demo is using for AU UserUpn
        }

        public AgenticAuthorization(IConnections connections)
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));
            _connections = connections;
        }

        public async Task<string> GetAgentInstanceTokenAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (!IsAgenticRequest(turnContext))
            {
                return null;
            }

            var connection = _connections.GetTokenProvider(turnContext.Identity, "agentic");
            if (connection is IAgenticTokenProvider agenticTokenProvider)
            {
                return await agenticTokenProvider.GetAgenticInstanceTokenAsync(GetAgentInstanceId(turnContext), cancellationToken);
            }

            throw new InvalidOperationException("Connection doesn't support IAgenticTokenProvider");
        }

        public async Task<string> GetAgentUserTokenAsync(ITurnContext turnContext, IList<string> scopes, CancellationToken cancellationToken = default)
        {
            if (!IsAgenticRequest(turnContext) || string.IsNullOrEmpty(GetAgentUser(turnContext)))
            {
                return null;
            }

            var connection = _connections.GetTokenProvider(turnContext.Identity, "agentic");
            if (connection is IAgenticTokenProvider agenticTokenProvider)
            {
                return await agenticTokenProvider.GetAgenticUserTokenAsync(GetAgentInstanceId(turnContext), GetAgentUser(turnContext), scopes, cancellationToken);
            }

            throw new InvalidOperationException("Connection doesn't support IAgenticTokenProvider");
        }
    }
}

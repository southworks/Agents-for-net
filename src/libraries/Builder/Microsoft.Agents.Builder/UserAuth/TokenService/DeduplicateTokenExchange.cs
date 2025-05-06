// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Storage;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    /// <summary>
    /// If the activity name is signin/tokenExchange, this will attempt to
    /// exchange the token, and deduplicate the incoming call, ensuring only one
    /// exchange request is processed.
    /// </summary>
    /// <remarks>
    /// This is only for Teams or SharePoint channels.
    /// If a user is signed into multiple Teams clients, the Agent could receive a
    /// "signin/tokenExchange" from each client. Each token exchange request for a
    /// specific user login will have an identical Activity.Value.Id.
    /// 
    /// Only one of these token exchange requests should be processed by the Agent.
    /// The others return <see cref="HttpStatusCode.PreconditionFailed"/>.
    /// For a distributed Agent in production, this requires a distributed storage
    /// ensuring only one token exchange is processed. This supports
    /// CosmosDb storage found in Microsoft.Agents.Storage.CosmosDb, 
    /// Microsoft.Agents.Storage.Blobs, or MemoryStorage for
    /// local development. IStorage's ETag implementation for token exchange activity
    /// deduplication.
    /// </remarks>
    internal class DeduplicateTokenExchange(IStorage storage)
    {

        /// <summary>
        /// Deduplicates exchange token requests.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="storage"></param>
        /// <param name="connectionName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>true to continue processing the turn.</returns>
        public async Task<bool> DedupeAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (ShouldExchange(turnContext))
            {
                // Only one token exchange should proceed from here. Deduplication is performed second because in the case
                // of failure due to consent required, every caller needs to receive the InvokeResponse
                if (!await DeduplicatedTokenExchangeIdAsync(turnContext, storage, cancellationToken).ConfigureAwait(false))
                {
                    // Stop if the token has already been exchanged.
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldExchange(ITurnContext turnContext)
        {
            // Teams
            if (string.Equals(Channels.Msteams, turnContext.Activity.ChannelId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(SignInConstants.TokenExchangeOperationName, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // SharePoint
            if (string.Equals(Channels.M365, turnContext.Activity.ChannelId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(SignInConstants.SharePointTokenExchange, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static async Task<bool> DeduplicatedTokenExchangeIdAsync(ITurnContext turnContext, IStorage storage, CancellationToken cancellationToken)
        {
            var id = turnContext.Activity.Value.ToJsonElements()["id"].ToString();

            // Create a StoreItem with Etag of the unique 'signin/tokenExchange' request
            var storeItem = new TokenStoreItem
            {
                ETag = id,
            };

            var storeItems = new Dictionary<string, object> { { TokenStoreItem.GetStorageKey(turnContext, id), storeItem } };
            try
            {
                // Writing the IStoreItem with ETag of unique id will succeed only once
                await storage.WriteAsync(storeItems, cancellationToken).ConfigureAwait(false);
            }
            catch (EtagException)
            {
                // Do NOT proceed processing this message, some other thread or machine already has processed it.

                // Send 200 invoke response.
                await turnContext.SendActivityAsync(
                    new Activity
                    {
                        Type = ActivityTypes.InvokeResponse,
                        Value = new InvokeResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                        },
                    }, 
                    cancellationToken).ConfigureAwait(false);
                return false;
            }

            return true;
        }

        private class TokenStoreItem : IStoreItem
        {
            public string ETag { get; set; }

            public static string GetStorageKey(ITurnContext turnContext, string id)
            {
                return $"oauth/{turnContext.Activity.ChannelId}/{turnContext.Activity.Conversation?.Id}/{id}";
            }
        }
    }
}

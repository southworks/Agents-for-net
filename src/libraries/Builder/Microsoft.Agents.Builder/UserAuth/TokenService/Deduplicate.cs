// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Microsoft.Identity.Client.Extensions.Msal;
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// </remarks>
    internal class Deduplicate(IStorage storage)
    {

        /// <summary>
        /// Deduplicates exchange token requests.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="storage"></param>
        /// <param name="connectionName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>true to continue processing the turn.</returns>
        public async Task<bool> ProceedWithExchangeAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (ShouldDeduplicate(turnContext))
            {
                // Only one token exchange should proceed from here. Deduplication is performed second because in the case
                // of failure due to consent required, every caller needs to receive the InvokeResponse
                if (await IsDuplicateTokenExchangeIdAsync(turnContext, storage, cancellationToken).ConfigureAwait(false))
                {
                    // Do NOT proceed processing this message, some other thread or machine already has processed it.
                    // But we must send an InvokeResponse
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

                    // Stop if the token has already been exchanged.
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldDeduplicate(ITurnContext turnContext)
        {
            // Teams
            if (turnContext.Activity.ChannelId.IsParentChannel(Channels.Msteams)
                && string.Equals(SignInConstants.TokenExchangeOperationName, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // SharePoint
            if (turnContext.Activity.ChannelId == Channels.M365
                && string.Equals(SignInConstants.SharePointTokenExchange, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        // true if the Invoke is a duplicate
        private static async Task<bool> IsDuplicateTokenExchangeIdAsync(ITurnContext turnContext, IStorage storage, CancellationToken cancellationToken) 
        {
            var id = turnContext.Activity.Value.ToJsonElements()["id"].ToString();
            var key = TokenStoreItem.GetStorageKey(turnContext, id);

            var items = await storage.ReadAsync<TokenStoreItem>(new string[] { key }, cancellationToken).ConfigureAwait(false);
            var item = items.FirstOrDefault().Value;

            var changes = new Dictionary<string, object>
            {
                [key] = new TokenStoreItem { ETag = item?.ETag },
            };

            if (item == null)
            {
                // Create the item in the Storage for the first time to gather the ETag, to then use it later for concurrency control and avoid deduplication.
                var result = await storage.WriteAsync(changes, cancellationToken).ConfigureAwait(false);
                (changes[key] as TokenStoreItem).ETag = result[key].ETag;
            }

            try
            {
                await storage.WriteAsync(changes, cancellationToken).ConfigureAwait(false);
            }
            catch (EtagException)
            {
                return true;
            }

            return false;
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

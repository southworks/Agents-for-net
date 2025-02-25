// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.UserAuth.TokenService
{
    public class UserTokenClientWrapper
    {
        public static async Task<SignInResource> GetSignInResourceAsync(ITurnContext context, string connectionName, CancellationToken cancellationToken)
        {
            IUserTokenClient userTokenClient = GetUserTokenClient(context);
            return await userTokenClient.GetSignInResourceAsync(connectionName, context.Activity, null, cancellationToken);
        }

        public static async Task<TokenResponse> GetUserTokenAsync(ITurnContext context, string connectionName, string magicCode, CancellationToken cancellationToken)
        {
            IUserTokenClient userTokenClient = GetUserTokenClient(context);
            return await userTokenClient.GetUserTokenAsync(context.Activity.From.Id, connectionName, context.Activity.ChannelId, magicCode, cancellationToken);
        }

        public static async Task<TokenResponse> ExchangeTokenAsync(ITurnContext context, string connectionName, TokenExchangeRequest tokenExchangeRequest, CancellationToken cancellationToken)
        {
            IUserTokenClient userTokenClient = GetUserTokenClient(context);
            return await userTokenClient.ExchangeTokenAsync(context.Activity.From.Id, connectionName, context.Activity.ChannelId, tokenExchangeRequest, cancellationToken);
        }

        public static async Task SignOutUserAsync(ITurnContext context, string connectionName, CancellationToken cancellationToken)
        {
            IUserTokenClient userTokenClient = GetUserTokenClient(context);
            await userTokenClient.SignOutUserAsync(context.Activity.From.Id, connectionName, context.Activity.ChannelId, cancellationToken);
        }

        private static IUserTokenClient GetUserTokenClient(ITurnContext context)
        {
            IUserTokenClient userTokenClient = context.Services.Get<IUserTokenClient>();
            if (userTokenClient == null)
            {
                throw new NotSupportedException("IUserTokenClient is not supported by the current adapter");
            }
            return userTokenClient;
        }
    }
}

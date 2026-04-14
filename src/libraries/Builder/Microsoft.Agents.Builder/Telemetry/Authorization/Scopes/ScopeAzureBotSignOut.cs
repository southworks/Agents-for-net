using Micr

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    /// <summary>
    /// A <see cref="ScopeAuthorizationRequest"/> that traces an Azure Bot Framework
    /// OAuth sign-out operation.
    /// </summary>
    internal class ScopeAzureBotSignOut : ScopeAuthorizationRequest
    {

        private readonly ITurnContext _turnContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeAzureBotSignOut"/> class.
        /// </summary>
        /// <param name="authHandlerId">The identifier of the authentication handler processing the request.</param>
        public ScopeAzureBotSignOut(string authHandlerId, string exchangeConnection, ITurnContext turnContext) : base(Constants.ScopeAzureBotSignOut, authHandlerId, exchangeConnection)
        {
            _turnContext = turnContext;
        }

        protected override void Callback(Activity telemetryActivity, double duration, Exception? exception)
        {
            base.Callback(telemetryActivity, duration, exception);
            telemetryActivity.SetTag(TagNames.ActivityChannelId, _turnContext.Activity.ChannelId?.ToString());
        }
    }
}
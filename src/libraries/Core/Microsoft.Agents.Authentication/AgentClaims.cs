// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;


namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// Provides utility methods for working with claims in the context of Agent authentication.
    /// </summary>
    public static class AgentClaims
    {
        public static bool IsExchangeableToken(JwtSecurityToken jwtToken)
        {
            AssertionHelpers.ThrowIfNull(jwtToken, nameof(jwtToken));

            var idtyp = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "idtyp")?.Value;
            if ("user".Equals(idtyp))
            {
                return false;
            }

            var aud = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "aud")?.Value;
            var appId = GetAppId(jwtToken);  // this will use either "appid" or "azp" based on 'ver'
            return (bool)(aud?.Contains(appId));
        }

        public static string GetAppId(JwtSecurityToken jwtToken)
        {
            AssertionHelpers.ThrowIfNull(jwtToken, nameof(jwtToken));

            string appIdClaim;
            var ver = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "ver")?.Value ?? "1.0";
            if (ver.Equals("1.0"))
            {
                appIdClaim = jwtToken.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AppIdClaim)?.Value;
            }
            else
            {
                appIdClaim = jwtToken.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AzpClaim)?.Value;
            }

            return appIdClaim;
        }

        /// <summary>
        /// Retrieves the AppId from the given claims identity.
        /// </summary>
        /// <param name="claimsIdentity">The claims identity containing the token information.</param>
        /// <returns>The AppId as a string, or null if not found.</returns>
        /// <remarks>
        /// For requests the AppId is in the Audience claim of the JWT token.
        /// </remarks>
        [Obsolete("GetAppId is deprecated, please use GetIncomingAudienceClaim instead.")]
        public static string GetAppId(ClaimsIdentity claimsIdentity)
        {
            return GetIncomingAudienceClaim(claimsIdentity);
        }

        /// <summary>
        /// Retrieves the AppId from the given claims identity.
        /// </summary>
        /// <param name="claimsIdentity">The claims identity containing the token information.</param>
        /// <returns>The AppId as a string, or null if not found.</returns>
        /// <remarks>
        /// For requests the AppId is in the Audience claim of the JWT token.
        /// </remarks>
        public static string GetIncomingAudienceClaim(ClaimsIdentity claimsIdentity)
        {
            // Verify we have a sensible Claims Identity
            AssertionHelpers.ThrowIfNull(claimsIdentity, nameof(claimsIdentity));

            // For requests from channel App Id is in Audience claim of JWT token. For emulator it is in AppId claim. For
            // unauthenticated requests we have anonymous claimsIdentity provided auth is disabled.
            // For Activities coming from Emulator AppId claim contains the Agent's AAD AppId.
            var appIdClaim = claimsIdentity.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AudienceClaim);
            appIdClaim ??= claimsIdentity.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AppIdClaim);

            return appIdClaim?.Value;
        }

        /// <summary>
        /// Retrieves the audience from the given claims identity.
        /// </summary>
        /// <param name="claimsIdentity">The claims identity containing the token information.</param>
        /// <returns>The audience as a string, or null if not found.</returns>
        /// <remarks>
        /// For requests the audience is in the Audience claim of the JWT token.
        /// </remarks>
        public static string GetIncomingAudience(this ClaimsIdentity claimsIdentity)
        {
            return GetIncomingAudienceClaim(claimsIdentity);
        }

        /// <summary>
        /// Gets the outgoing AppId from a claims list.
        /// </summary>
        /// <remarks>
        /// In v1 tokens the AppId is in the the <see cref="AuthenticationConstants.AppIdClaim"/> claim.
        /// In v2 tokens the AppId is in the azp <see cref="AuthenticationConstants.AuthorizedParty"/> claim.
        /// If the <see cref="AuthenticationConstants.VersionClaim"/> is not present, this method will attempt to
        /// obtain the attribute from the <see cref="AuthenticationConstants.AppIdClaim"/> or if present.
        /// </remarks>
        /// <param name="identity">The Agent identity</param>
        /// <returns>The value of the appId claim if found (null if it can't find a suitable claim).</returns>
        public static string GetOutgoingAppIdClaim(ClaimsIdentity identity)
        {
            // Verify we have a sensible Claims Identity
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));

            var claimsList = identity.Claims;
            string appId = null;

            // Depending on Version, the is either in the
            // appid claim (Version 1) or the Authorized Party claim (Version 2).
            var tokenVersion = claimsList.FirstOrDefault(claim => claim.Type == AuthenticationConstants.VersionClaim)?.Value;
            if (string.IsNullOrWhiteSpace(tokenVersion) || tokenVersion == "1.0")
            {
                // either no Version or a version of "1.0" means we should look for
                // the claim in the "appid" claim.
                var appIdClaim = claimsList.FirstOrDefault(c => c.Type == AuthenticationConstants.AppIdClaim);
                appId = appIdClaim?.Value;
            }
            else if (tokenVersion == "2.0")
            {
                // "2.0" puts the AppId in the "azp" claim.
                var appZClaim = claimsList.FirstOrDefault(c => c.Type == AuthenticationConstants.AuthorizedParty);
                appId = appZClaim?.Value;
            }

            return appId;
        }

        /// <summary>
        /// Gets the outgoing AppId from a claims list.
        /// </summary>
        /// <remarks>
        /// In v1 tokens the AppId is in the the <see cref="AuthenticationConstants.AppIdClaim"/> claim.
        /// In v2 tokens the AppId is in the azp <see cref="AuthenticationConstants.AuthorizedParty"/> claim.
        /// If the <see cref="AuthenticationConstants.VersionClaim"/> is not present, this method will attempt to
        /// obtain the attribute from the <see cref="AuthenticationConstants.AppIdClaim"/> or if present.
        /// </remarks>
        /// <param name="identity">The Agent identity</param>
        /// <returns>The value of the appId claim if found (null if it can't find a suitable claim).</returns>
        public static string GetOutgoingAppId(this ClaimsIdentity identity)
        {
            return GetOutgoingAppIdClaim(identity);
        }

        /// <summary>
        /// Checks if the given list of claims represents a Agent claim (not coming from ABS/SMBA).
        /// </summary>
        /// <remarks>
        /// A Agent claim should contain:
        ///     An <see cref="AuthenticationConstants.VersionClaim"/> claim.
        ///     An <see cref="AuthenticationConstants.AudienceClaim"/> claim.
        ///     An <see cref="AuthenticationConstants.AppIdClaim"/> claim (v1) or an a <see cref="AuthenticationConstants.AuthorizedParty"/> claim (v2).
        /// And the appId claim should be different than the audience claim.
        /// When a channel (webchat, teams, etc.) invokes an Agent, the <see cref="AuthenticationConstants.AudienceClaim"/>
        /// is set to <see cref="AuthenticationConstants.BotFrameworkTokenIssuer"/> but when an Agent calls another Agent,
        /// the audience claim is set to the appId of the Agent being invoked.
        /// The protocol supports v1 and v2 tokens:
        /// For v1 tokens, the  <see cref="AuthenticationConstants.AppIdClaim"/> is present and set to the app Id of the calling Agent.
        /// For v2 tokens, the  <see cref="AuthenticationConstants.AuthorizedParty"/> is present and set to the app Id of the calling Agent.
        /// </remarks>
        /// <param name="claims">A list of claims.</param>
        /// <returns>True if the list of claims is an Agent claim, false if is not.</returns>
        public static bool IsAgentClaim(ClaimsIdentity claims)
        {
            AssertionHelpers.ThrowIfNull(claims, nameof(claims));

            var claimsList = claims.Claims;

            var version = claimsList.FirstOrDefault(claim => claim.Type == AuthenticationConstants.VersionClaim);
            if (string.IsNullOrWhiteSpace(version?.Value))
            {
                // Must have a version claim.
                return false;
            }

            var audience = claimsList.FirstOrDefault(claim => claim.Type == AuthenticationConstants.AudienceClaim)?.Value;
            if (string.IsNullOrWhiteSpace(audience) 
                || AuthenticationConstants.BotFrameworkTokenIssuer.Equals(audience, StringComparison.OrdinalIgnoreCase)
                || AuthenticationConstants.GovBotFrameworkTokenIssuer.Equals(audience, StringComparison.OrdinalIgnoreCase))
            {
                // The audience is https://api.botframework.com or https://api.botframework.us and not an appId.
                return false;
            }

            var appId = GetOutgoingAppIdClaim(claims);
            if (string.IsNullOrWhiteSpace(appId))
            {
                return false;
            }

            // Agent claims must contain and app ID and the AppID must be different than the audience.
            return appId != audience;
        }

        /// <summary>
        /// Determines whether the specified identity contains claims that identify the user as an agent.
        /// </summary>
        /// <remarks>This method checks for the presence of claims that designate the user as an agent.
        /// Ensure that the ClaimsIdentity is populated with the relevant claims before calling this method.</remarks>
        /// <param name="claims">The identity to evaluate for agent claims. This parameter cannot be null.</param>
        /// <returns>true if the identity contains an agent claim; otherwise, false.</returns>
        public static bool IsAgent(this ClaimsIdentity claims)
        {
            return IsAgentClaim(claims);
        }

        /// <summary>
        /// Retrieves the audience for an outgoing token from the given the incoming identity.
        /// </summary>
        /// <param name="identity">The claims identity containing the token information.</param>
        /// <returns>The token audience as a string.</returns>
        [Obsolete("GetTokenAudience is deprecated, please use GetOutgoingAudienceClaim instead.")]
        public static string GetTokenAudience(ClaimsIdentity identity)
        {
            return GetOutgoingAudienceClaim(identity);
        }

        /// <summary>
        /// Retrieves the audience for an outgoing token from the given the incoming identity.
        /// </summary>
        /// <param name="identity">The claims identity containing the token information.</param>
        /// <returns>The token audience as a string.</returns>
        public static string GetOutgoingAudienceClaim(ClaimsIdentity identity)
        {
            return AgentClaims.IsAgentClaim(identity)
                ? $"api://{AgentClaims.GetOutgoingAppIdClaim(identity)}"
                : AuthenticationConstants.BotFrameworkAudience;
        }

        /// <summary>
        /// Retrieves the audience for an outgoing token from the given the incoming identity.
        /// </summary>
        /// <param name="identity">The claims identity containing the token information.</param>
        /// <returns>The token audience as a string.</returns>
        public static string GetOutgoingAudience(this ClaimsIdentity identity)
        {
            return GetOutgoingAudienceClaim(identity);
        }

        /// <summary>
        /// Determines whether the specified claims identity represents a government Bot Framework claim.
        /// </summary>
        /// <remarks>This method checks the audience claim within the provided claims identity and
        /// compares it against the expected government Bot Framework token issuer. Use this method to distinguish
        /// government Bot Framework tokens from standard tokens when handling authentication.</remarks>
        /// <param name="claims">The claims identity to evaluate. Cannot be null.</param>
        /// <returns>true if the claims identity corresponds to a government Bot Framework claim; otherwise, false.</returns>
        public static bool IsGovBotFrameworkClaim(ClaimsIdentity claims)
        {
            AssertionHelpers.ThrowIfNull(claims, nameof(claims));
            var audience = claims.Claims.FirstOrDefault(claim => claim.Type == AuthenticationConstants.AudienceClaim)?.Value;
            return AuthenticationConstants.GovBotFrameworkTokenIssuer.Equals(audience, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the specified claims identity represents a Bot Framework claim.
        /// </summary>
        /// <remarks>This method checks the audience claim within the provided claims identity and
        /// compares it against the expected government Bot Framework token issuer. Use this method to distinguish
        /// Bot Framework tokens from standard tokens when handling authentication.</remarks>
        /// <param name="claims">The claims identity to evaluate. Cannot be null.</param>
        /// <returns>true if the claims identity corresponds to a Bot Framework claim; otherwise, false.</returns>
        public static bool IsPublicBotFrameworkClaim(ClaimsIdentity claims)
        {
            AssertionHelpers.ThrowIfNull(claims, nameof(claims));
            var audience = claims.Claims.FirstOrDefault(claim => claim.Type == AuthenticationConstants.AudienceClaim)?.Value;
            return AuthenticationConstants.BotFrameworkTokenIssuer.Equals(audience, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the specified claims identity contains a valid Bot Framework claim.
        /// </summary>
        /// <remarks>This method checks for both public and government Bot Framework claims to ascertain
        /// the validity of the claims identity.</remarks>
        /// <param name="claims">The claims identity to evaluate for Bot Framework claims. This should represent the user's claims in the
        /// context of the Bot Framework.</param>
        /// <returns>true if the claims identity contains a valid Bot Framework claim; otherwise, false.</returns>
        public static bool IsBotFrameworkClaim(ClaimsIdentity claims)
        {
            return IsPublicBotFrameworkClaim(claims) || IsGovBotFrameworkClaim(claims);
        }

        /// <summary>
        /// Determines whether the specified claims identity represents a Bot Framework user.   
        /// </summary>
        /// <remarks>This method checks for specific claims that indicate the identity belongs to a Bot
        /// Framework user. Use this method to distinguish Bot Framework identities from other types of claims
        /// identities.</remarks>
        /// <param name="claims">The claims identity to evaluate for Bot Framework claims. This parameter cannot be null.</param>
        /// <returns>true if the claims identity contains Bot Framework claims; otherwise, false.</returns>
        public static bool IsBotFramework(this ClaimsIdentity claims)
        {
            return IsBotFrameworkClaim(claims);
        }

        /// <summary>
        /// Retrieves the token scopes from the given claims identity.
        /// </summary>
        /// <param name="identity">The claims identity containing the token information.</param>
        /// <param name="defaultABSScopes">Normally the IAccessTokenProvider will use what's in settings.  This is what we want for 
        /// Azure Bot Service since that can change for Public vs Gov, etc...  For agent scenarios though, it's always defaulted
        /// to "{{appid}}/.default".</param>
        /// <returns>A list of token scopes.</returns>
        [Obsolete("GetTokenScopes is deprecated, please use GetOutgoingTokenScopes instead.")]
        public static IList<string> GetTokenScopes(ClaimsIdentity identity, bool defaultABSScopes = false)
        {
            return GetOutgoingTokenScopes(identity, defaultABSScopes);
        }

        /// <summary>
        /// Retrieves the token scopes from the given claims identity.
        /// </summary>
        /// <param name="identity">The claims identity containing the token information.</param>
        /// <param name="defaultABSScopes">Normally the IAccessTokenProvider will use what's in settings.  This is what we want for 
        /// Azure Bot Service since that can change for Public vs Gov, etc...  For agent scenarios though, it's always defaulted
        /// to "{{appid}}/.default".</param>
        /// <returns>A list of token scopes.</returns>
        public static IList<string> GetOutgoingTokenScopes(this ClaimsIdentity identity, bool defaultABSScopes = false)
        {
            return AgentClaims.IsAgentClaim(identity)
                ? [$"{AgentClaims.GetOutgoingAppIdClaim(identity)}/.default"]
                : defaultABSScopes ? DefaultAzureBotServiceScopes(identity) : null;
        }

        /// <summary>
        /// Retrieves the token scopes from the given claims identity.
        /// </summary>
        /// <param name="identity">The claims identity containing the token information.</param>
        /// <param name="defaultABSScopes">Normally the IAccessTokenProvider will use what's in settings.  This is what we want for 
        /// Azure Bot Service since that can change for Public vs Gov, etc...  For agent scenarios though, it's always defaulted
        /// to "{{appid}}/.default".</param>
        /// <returns>A list of token scopes.</returns>
        public static IList<string> GetOutgoingScopes(this ClaimsIdentity identity, bool defaultABSScopes = false)
        {
            return GetOutgoingTokenScopes(identity, false);
        }

        /// <summary>
        /// Returns the default Azure Bot Service scopes based on the claims identity. If the claims identity corresponds to a government Bot Framework 
        /// claim, it returns the government-specific default scope; otherwise, it returns the standard default scope.
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        public static IList<string> DefaultAzureBotServiceScopes(ClaimsIdentity identity)
        {
            return IsGovBotFrameworkClaim(identity) ? [AuthenticationConstants.GovBotFrameworkDefaultScope] : [AuthenticationConstants.BotFrameworkDefaultScope];
        }   

        /// <summary>
        /// Determines whether anonymous access is allowed based on the given claims identity.
        /// </summary>
        /// <param name="identity">The claims identity to evaluate.</param>
        /// <returns>True if anonymous access is allowed, otherwise false.</returns>
        public static bool AllowAnonymous(ClaimsIdentity identity)
        {
            return identity != null && !identity.IsAuthenticated && !identity.Claims.Any();
        }

        /// <summary>
        /// Creates an ingress Agent ClaimsIdentity.
        /// </summary>
        /// <param name="audience">The aud of the claim.  Typically the ClientId of the Agent.</param>
        /// <param name="anonymous"></param>
        /// <param name="appId">The appId of the sender. Not supplying the appId will work correctly against Azure Bot Service, but not to another Agent.</param>
        /// <returns></returns>
        public static ClaimsIdentity CreateIdentity(string audience, bool anonymous = false, string appId = null)
        {
            if (anonymous)
            {
                return new ClaimsIdentity();
            }

            IEnumerable<Claim> claims = [
                    new(AuthenticationConstants.AudienceClaim, audience),
                    new(AuthenticationConstants.VersionClaim, "1.0")
                ];

            if (!string.IsNullOrEmpty(appId))
            {
                claims = claims.Append(new(AuthenticationConstants.AppIdClaim, appId));
            }

            return new ClaimsIdentity(claims);
        }
    }
}

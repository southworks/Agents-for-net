﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using Xunit;

namespace Microsoft.Agents.Auth.Tests
{
    public class BotClaimsTests
    {
        [Fact]
        public void GetAppId_ThrowIfNull()
        {
            Assert.Throws<ArgumentNullException>(() => BotClaims.GetAppId(null));
        }

        [Fact]
        public void GetAppId_NullIfNoClaims()
        {
            ClaimsIdentity identity = new();
            Assert.Null(BotClaims.GetAppId(identity));
        }

        [Fact]
        public void GetAppId_NullIfNoAudienceOrAppIDClaims()
        {
            // Setup a claim set with a claim, but not with a valid Audience or AppID claim. 
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "Name1")
            };
            ClaimsIdentity identity = new(claims);

            // Verify asking for an AppId on a claimset without the right claims throws an exception
            Assert.Null(BotClaims.GetAppId(identity));
        }
        [Fact]
        public void GetAppId_ThrowIf2AudienceClaims()
        {
            // Setup a claim set with a 2 claims, both of which as Audience claims.
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AudienceClaim, "claim1"),
                new(AuthenticationConstants.AudienceClaim, "claim2")
            };

            ClaimsIdentity identity = new(claims);

            // Verify asking for an AppId on a claimset without the right claims throws an exception
            Assert.Throws<InvalidOperationException>(() => BotClaims.GetAppId(identity));
        }

        [Fact]
        public void GetAppId_ThrowIf2AppIdClaims()
        {
            // Setup a claim set with a 2 claims, both of which are AppId claims.
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AppIdClaim, "claim1"),
                new(AuthenticationConstants.AppIdClaim, "claim2")
            };

            ClaimsIdentity identity = new(claims);

            // Verify asking for an AppId on a claimset without the right claims throws an exception
            Assert.Throws<InvalidOperationException>(() => BotClaims.GetAppId(identity));
        }

        [Fact]
        public void GetAppId_AudienceClaim()
        {
            // Setup a claim set with a 2 claims, both of which are AppId claims.
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AudienceClaim, "claim1"),
            };

            ClaimsIdentity identity = new(claims);

            // Verify asking for an AppId on a claimset without the right claims throws an exception
            Assert.Equal("claim1", BotClaims.GetAppId(identity));
        }

        [Fact]
        public void GetAppId_AppIdClaim()
        {
            // Setup a claim set with a 2 claims, both of which are AppId claims.
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AppIdClaim, "claim1"),
            };

            ClaimsIdentity identity = new(claims);

            // Verify asking for an AppId on a claimset without the right claims throws an exception
            Assert.Equal("claim1", BotClaims.GetAppId(identity));
        }

        [Fact]
        public void GetAppId_AudienceAndAppIdClaim()
        {
            // Setup a claim set with a 2 claims. Verify Audiance is chosen over AppId. 
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AppIdClaim, "claim1"),
                new(AuthenticationConstants.AudienceClaim, "claim2"),
            };

            ClaimsIdentity identity = new(claims);

            // Verify asking for an AppId on a claimset with both claims returns the Audiance claim.
            Assert.Equal("claim2", BotClaims.GetAppId(identity));
        }

        [Fact]
        public void GetOutgoingAppId_ThrowIfNull()
        {
            Assert.Throws<ArgumentNullException>(() => BotClaims.GetOutgoingAppId(null));
        }

        [Fact]
        public void GetOutgoingAppId_NoVersionClaim()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "Name1")
            };

            Assert.Null(BotClaims.GetOutgoingAppId(claims));
        }

        [Fact]
        public void GetOutgoingAppId_MultipleVersionClaims()
        {
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.VersionClaim, "claim1"),
                new(AuthenticationConstants.VersionClaim, "claim2")
            };

            Assert.Null(BotClaims.GetOutgoingAppId(claims));
        }

        [Fact]
        public void GetOutgoingAppId_InvalidVersionNumber()
        {
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.VersionClaim, "3.0"),                
            };

            Assert.Null(BotClaims.GetOutgoingAppId(claims));
        }

        [Fact]
        public void GetOutgoingAppId_ValidVersionClaimNullVersionNoAppId()
        {
            // this tests sets up a claimset with a version claim with an empty string. There is no AppIdClaim, so this should fail. 
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.VersionClaim, string.Empty),
            };

            Assert.Null(BotClaims.GetOutgoingAppId(claims));
        }
        [Fact]
        public void GetOutgoingAppId_ValidVersionClaimNullVersionAppId()
        {
            // this tests sets up a claimset with a empty version and an AppId Claim. This is a valid combination. 
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.VersionClaim, string.Empty),
                new(AuthenticationConstants.AppIdClaim, "appId")                
            };

            Assert.Equal("appId", BotClaims.GetOutgoingAppId(claims));
        }
        
        [Fact]
        public void GetOutgoingAppId_ValidV1VersionAppId()
        {
            // this tests sets up a claimset with a version of v1 and an AppId Claim. This is a valid combination. 
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.VersionClaim, "1.0"),
                new(AuthenticationConstants.AppIdClaim, "appId")
            };

            Assert.Equal("appId", BotClaims.GetOutgoingAppId(claims));
        }

        [Fact]
        public void GetOutgoingAppId_ValidV2VersionWrongClaim()
        {
            // this tests sets up a claimset with a version claim with an empty string. There is no AppIdClaim, so this should fail. 
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AppIdClaim, "appId") // not a valid claim on a v2 token
            };

            Assert.Null(BotClaims.GetOutgoingAppId(claims));            
        }


        [Fact]
        public void GetOutgoingAppId_ValidV2VersionClaimWithAuthorizedParty()
        {
            static IEnumerable<Claim> GetClaims() // Use this function to cover the ToList() method.
            {
                // this tests sets up a claimset with a version claim "2.0" and an AuthorizedParty Claim.
                yield return new Claim(AuthenticationConstants.VersionClaim, "2.0");
                yield return new Claim(AuthenticationConstants.AuthorizedParty, "appId");
            };

            Assert.Equal("appId", BotClaims.GetOutgoingAppId(GetClaims()));
        }

        [Fact]
        public void IsBotClaim_ThrowOnNullClaimset()
        {
            Assert.Throws<ArgumentNullException>(() => BotClaims.IsBotClaim(null));
        }

        [Fact]
        public void IsBotClaim_ValidClaimset()
        {
            // Setup a valid claim set
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "party"),
                new(AuthenticationConstants.AudienceClaim, "audience")
            };

            Assert.True(BotClaims.IsBotClaim(claims));
        }

        [Fact]
        public void IsBotClaim_InvalidClaimset()
        {
            // Setup a valid claim set
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "party"), // if these 2 match, the claim set doesn't qualify
                new(AuthenticationConstants.AudienceClaim, "party") // if these 2 match, the claim set doesn't qualify
            };

            Assert.False(BotClaims.IsBotClaim(claims));
        }

        [Fact]
        public void IsBotClaim_InvalidClaimsetByAudiance()
        {
            // Setup a valid claim set
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "party"), 

                // For some reason, this is invalid. Coding it here to make sure behavior doesn't change. 
                new(AuthenticationConstants.AudienceClaim, AuthenticationConstants.BotFrameworkTokenIssuer) 
            };

            Assert.False(BotClaims.IsBotClaim(claims));
        }

        [Fact]
        public void IsBotClaim_ShouldReturnFalseOnNoVersion()
        {
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AudienceClaim, "audience"),
                new(AuthenticationConstants.AppIdClaim, "appId")
            };

            Assert.False(BotClaims.IsBotClaim(claims));
        }

        [Fact]
        public void IsBotClaim_ShouldReturnFalseOnNoOutgoingAppId()
        {
            // Setup an invalid claim set
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AudienceClaim, "audience"),
                new(AuthenticationConstants.AppIdClaim, "appId") // not a valid claim on a v2 token
            };

            Assert.False(BotClaims.IsBotClaim(claims));
        }

        [Fact]
        public void GetTokenAudience_ShouldSetAudienceInUri()
        {
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "appId")
            };

            var audience = BotClaims.GetTokenAudience(claims);

            Assert.Equal("app://appId", audience);
        }
    }
}

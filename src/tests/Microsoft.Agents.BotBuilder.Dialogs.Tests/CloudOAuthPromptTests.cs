﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.State;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core;
using Microsoft.Recognizers.Text;

namespace Microsoft.Agents.BotBuilder.Dialogs.Tests
{
    public class CloudOAuthPromptTests
    {
        [Fact]
        public async Task OAuthPromptBeginLoggedIn()
        {
            // Arrange
            var userId = "user-id";
            var connectionName = "connection-name";
            var channelId = "channel-id";
            string magicCode = null;

            // Arrange the Adapter.
            var mockConnector = new Mock<IConnectorClient>();

            var mockUserTokenClient = new Mock<IUserTokenClient>();
            mockUserTokenClient.Setup(
                x => x.GetUserTokenAsync(It.Is<string>(s => s == userId), It.Is<string>(s => s == connectionName), It.Is<string>(s => s == channelId), It.Is<string>(s => s == magicCode), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenResponse { ChannelId = channelId, ConnectionName = connectionName, Token = $"TOKEN" });

            var mockChannelServiceClientFactory = new Mock<IChannelServiceClientFactory>();
            mockChannelServiceClientFactory.Setup(
                x => x.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .ReturnsAsync(mockConnector.Object);
            mockChannelServiceClientFactory.Setup(
                x => x.CreateUserTokenClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockUserTokenClient.Object);

            var adapter = new TestCloudAdapter(mockChannelServiceClientFactory.Object);

            // Arrange the Dialogs.
            var convoState = new ConversationState(new MemoryStorage());

            // Add the OAuthPrompt.
            var oauthPromptSettings = new OAuthPromptSettings
            {
                ConnectionName = connectionName,
                Text = "Please sign in",
                Title = "Sign in",
            };

            // The on-turn callback.
            DialogTurnResult dialogTurnResult = null;
            BotCallbackHandler callback = async (turnContext, cancellationToken) =>
            {
                var dialogState = await convoState.GetPropertyAsync<DialogState>(turnContext, "DialogState", () => new DialogState(), cancellationToken);
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", oauthPromptSettings));

                var dialogContext = await dialogs.CreateContextAsync(turnContext);
                dialogTurnResult = await dialogContext.BeginDialogAsync("OAuthPrompt");
            };

            // The Activity for the turn. 
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                From = new ChannelAccount { Id = userId },
                Conversation = new ConversationAccount { Id = "conversation-id" },
                Text = "hi",
                ChannelId = channelId
            };

            // Act
            var invokeResponse = await adapter.ProcessAsync(new ClaimsIdentity(), activity, callback);

            // Assert
            Assert.Equal(DialogTurnStatus.Complete, dialogTurnResult.Status);
            Assert.IsType<TokenResponse>(dialogTurnResult.Result);
            Assert.Equal("TOKEN", ((TokenResponse)dialogTurnResult.Result).Token);
        }

        [Fact]
        public async Task OAuthPromptBeginNotLoggedIn_OAuthCard()
        {
            // Arrange
            var userId = "user-id";
            var connectionName = "connection-name";
            var channelId = "channel-id";
            string magicCode = null;

            // Arrange the Adapter.
            var mockConnector = new Mock<IConnectorClient>();
            var mockUserTokenClient = new Mock<IUserTokenClient>();

            mockUserTokenClient.Setup(
                x => x.GetUserTokenAsync(It.Is<string>(s => s == userId), It.Is<string>(s => s == connectionName), It.Is<string>(s => s == channelId), It.Is<string>(s => s == magicCode), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TokenResponse)null);
            mockUserTokenClient.Setup(
                x => x.GetSignInResourceAsync(It.Is<string>(s => s == connectionName), It.IsAny<Activity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SignInResource());

            var mockChannelServiceClientFactory = new Mock<IChannelServiceClientFactory>();
            mockChannelServiceClientFactory.Setup(
                x => x.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .ReturnsAsync(mockConnector.Object);
            mockChannelServiceClientFactory.Setup(
                x => x.CreateUserTokenClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockUserTokenClient.Object);

            var adapter = new TestCloudAdapter(mockChannelServiceClientFactory.Object);

            // Arrange the Dialogs.
            var convoState = new ConversationState(new MemoryStorage());

            // Add the OAuthPrompt.
            var oauthPromptSettings = new OAuthPromptSettings
            {
                ConnectionName = connectionName,
                Text = "Please sign in",
                Title = "Sign in",
            };

            // The on-turn callback.
            DialogTurnResult dialogTurnResult = null;
            BotCallbackHandler callback = async (turnContext, cancellationToken) =>
            {
                var dialogState = await convoState.GetPropertyAsync<DialogState>(turnContext, "DialogState", () => new DialogState(), cancellationToken);
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", oauthPromptSettings));

                var dialogContext = await dialogs.CreateContextAsync(turnContext);
                dialogTurnResult = await dialogContext.BeginDialogAsync("OAuthPrompt");
            };

            // The Activity for the turn. 
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                From = new ChannelAccount { Id = userId },
                Conversation = new ConversationAccount { Id = "conversation-id" },
                Text = "hi",
                ChannelId = channelId,
            };

            // Act
            var invokeResponse = await adapter.ProcessAsync(new ClaimsIdentity(), activity, callback);

            // Assert
            Assert.Equal(DialogTurnStatus.Waiting, dialogTurnResult.Status);
            Assert.Single(adapter.SentActivities);

            var sentActivity = adapter.SentActivities.First();
            Assert.Equal(ActivityTypes.Message, sentActivity.Type);
            Assert.Single(sentActivity.Attachments);

            var sentActivityAttachment = sentActivity.Attachments.First();
            Assert.Equal(OAuthCard.ContentType, sentActivityAttachment.ContentType);
            Assert.IsType<OAuthCard>(sentActivityAttachment.Content);

            var oauthCard = (OAuthCard)sentActivityAttachment.Content;
            Assert.Equal(oauthPromptSettings.Text, oauthCard.Text);
        }

        [Fact]
        public async Task OAuthPromptBeginNotLoggedIn_SignInCard()
        {
            // Arrange
            var userId = "user-id";
            var connectionName = "connection-name";
            var channelId = Channels.Skype;
            string magicCode = null;

            // Arrange the Adapter.
            var mockConnector = new Mock<IConnectorClient>();

            var mockUserTokenClient = new Mock<IUserTokenClient>();
            mockUserTokenClient.Setup(
                x => x.GetUserTokenAsync(It.Is<string>(s => s == userId), It.Is<string>(s => s == connectionName), It.Is<string>(s => s == channelId), It.Is<string>(s => s == magicCode), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TokenResponse)null);
            mockUserTokenClient.Setup(
                x => x.GetSignInResourceAsync(It.Is<string>(s => s == connectionName), It.IsAny<Activity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SignInResource());

            var mockChannelServiceClientFactory = new Mock<IChannelServiceClientFactory>();
            mockChannelServiceClientFactory.Setup(
                x => x.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .ReturnsAsync(mockConnector.Object);
            mockChannelServiceClientFactory.Setup(
                x => x.CreateUserTokenClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockUserTokenClient.Object);

            var adapter = new TestCloudAdapter(mockChannelServiceClientFactory.Object);

            // Arrange the Dialogs.
            var convoState = new ConversationState(new MemoryStorage());

            // Add the OAuthPrompt.
            var oauthPromptSettings = new OAuthPromptSettings
            {
                ConnectionName = connectionName,
                Text = "Please sign in",
                Title = "Sign in",
            };

            // The on-turn callback.
            DialogTurnResult dialogTurnResult = null;
            BotCallbackHandler callback = async (turnContext, cancellationToken) =>
            {
                var dialogState = await convoState.GetPropertyAsync<DialogState>(turnContext, "DialogState", () => new DialogState(), cancellationToken);
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", oauthPromptSettings));

                var dialogContext = await dialogs.CreateContextAsync(turnContext);
                dialogTurnResult = await dialogContext.BeginDialogAsync("OAuthPrompt");
            };

            // The Activity for the turn. 
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                From = new ChannelAccount { Id = userId },
                Conversation = new ConversationAccount { Id = "conversation-id" },
                Text = "hi",
                ChannelId = channelId,
            };

            // Act
            var invokeResponse = await adapter.ProcessAsync(new ClaimsIdentity(), activity, callback);

            // Assert
            Assert.Equal(DialogTurnStatus.Waiting, dialogTurnResult.Status);
            Assert.Single(adapter.SentActivities);

            var sentActivity = adapter.SentActivities.First();
            Assert.Equal(ActivityTypes.Message, sentActivity.Type);
            Assert.Single(sentActivity.Attachments);

            var sentActivityAttachment = sentActivity.Attachments.First();
            Assert.Equal(SigninCard.ContentType, sentActivityAttachment.ContentType);
            Assert.IsType<SigninCard>(sentActivityAttachment.Content);

            var signinCard = (SigninCard)sentActivityAttachment.Content;
            Assert.Equal(oauthPromptSettings.Text, signinCard.Text);
        }

        [Fact]
        public async Task OAuthPromptContinueWithTimeout()
        {
            // Arrange

            // Arrange the Adapter.
            var mockConnector = new Mock<IConnectorClient>();
            var mockUserTokenClient = new Mock<IUserTokenClient>();

            var mockChannelServiceClientFactory = new Mock<IChannelServiceClientFactory>();
            mockChannelServiceClientFactory.Setup(
                x => x.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .ReturnsAsync(mockConnector.Object);
            mockChannelServiceClientFactory.Setup(
                x => x.CreateUserTokenClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockUserTokenClient.Object);

            var adapter = new TestCloudAdapter(mockChannelServiceClientFactory.Object);

            // Arrange the Dialogs.
            var convoState = new ConversationState(new MemoryStorage());

            // Add the OAuthPrompt.
            var oauthPromptSettings = new OAuthPromptSettings
            {
                Text = "Please sign in",
                ConnectionName = "myConnection",
                Title = "Sign in",
            };

            // The on-turn callback.
            DialogTurnResult dialogTurnResult = null;
            BotCallbackHandler callback = async (turnContext, cancellationToken) =>
            {
                var dialogState = await convoState.GetPropertyAsync<DialogState>(turnContext, "DialogState", () => new DialogState(), cancellationToken);
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", oauthPromptSettings));

                var dialogContext = await dialogs.CreateContextAsync(turnContext);
                dialogContext.Stack.Insert(0, new DialogInstance { Id = "OAuthPrompt", State = new Dictionary<string, object> { { "expires", DateTime.UtcNow.AddMinutes(-5) } } });

                dialogTurnResult = await dialogContext.ContinueDialogAsync();
            };

            // The Activity for the turn.
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                From = new ChannelAccount { Id = "from-id" },
                Conversation = new ConversationAccount { Id = "conversation-id" },
                Text = "hi",
                ChannelId = "channel-id"
            };

            // Act
            var invokeResponse = await adapter.ProcessAsync(new ClaimsIdentity(), activity, callback);

            // Assert
            Assert.Equal(DialogTurnStatus.Complete, dialogTurnResult.Status);
            Assert.Null(dialogTurnResult.Result);
        }

        [Fact]
        public async Task OAuthPromptContinueWithMessage()
        {
            // Arrange
            var userId = "user-id";
            var connectionName = "connection-name";
            var channelId = "channel-id";
            var magicCode = "123456";

            // Arrange the Adapter.
            var mockConnector = new Mock<IConnectorClient>();

            var mockUserTokenClient = new Mock<IUserTokenClient>();
            mockUserTokenClient.Setup(
                x => x.GetUserTokenAsync(It.Is<string>(s => s == userId), It.Is<string>(s => s == connectionName), It.Is<string>(s => s == channelId), It.Is<string>(s => s == magicCode), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenResponse { ChannelId = channelId, ConnectionName = connectionName, Token = $"TOKEN" });

            var mockChannelServiceClientFactory = new Mock<IChannelServiceClientFactory>();
            mockChannelServiceClientFactory.Setup(
                x => x.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .ReturnsAsync(mockConnector.Object);
            mockChannelServiceClientFactory.Setup(
                x => x.CreateUserTokenClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockUserTokenClient.Object);

            var adapter = new TestCloudAdapter(mockChannelServiceClientFactory.Object);

            // Arrange the Dialogs.
            var convoState = new ConversationState(new MemoryStorage());

            // Add the OAuthPrompt.
            var oauthPromptSettings = new OAuthPromptSettings
            {
                Text = "Please sign in",
                ConnectionName = connectionName,
                Title = "Sign in",
            };

            // The on-turn callback.
            DialogTurnResult dialogTurnResult = null;
            BotCallbackHandler callback = async (turnContext, cancellationToken) =>
            {
                var dialogState = await convoState.GetPropertyAsync<DialogState>(turnContext, "DialogState", () => new DialogState(), cancellationToken);
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", oauthPromptSettings));

                var dialogContext = await dialogs.CreateContextAsync(turnContext);
                dialogContext.Stack.Insert(
                    0,
                    new DialogInstance
                    {
                        Id = "OAuthPrompt",
                        State = new Dictionary<string, object>
                        {
                            { "expires", DateTime.UtcNow.AddHours(8) },
                            { "caller", null },
                            { "state", new Dictionary<string, object> { { "AttemptCount", 0 } } },
                            { "options", new PromptOptions() }
                        }
                    });

                dialogTurnResult = await dialogContext.ContinueDialogAsync();
            };

            // The Activity for the turn. 
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                From = new ChannelAccount { Id = userId },
                Conversation = new ConversationAccount { Id = "conversation-id" },
                Text = magicCode,
                ChannelId = channelId
            };

            // Act
            var invokeResponse = await adapter.ProcessAsync(new ClaimsIdentity(), activity, callback);

            // Assert
            Assert.Equal(DialogTurnStatus.Complete, dialogTurnResult.Status);
            Assert.IsType<TokenResponse>(dialogTurnResult.Result);
            Assert.Equal("TOKEN", ((TokenResponse)dialogTurnResult.Result).Token);
        }

        [Fact]
        public async Task OAuthPromptContinueWithEvent()
        {
            // Arrange
            var userId = "user-id";
            var connectionName = "connection-name";
            var channelId = "channel-id";

            // Arrange the Adapter.
            var mockConnector = new Mock<IConnectorClient>();
            var mockUserTokenClient = new Mock<IUserTokenClient>();

            var mockChannelServiceClientFactory = new Mock<IChannelServiceClientFactory>();
            mockChannelServiceClientFactory.Setup(
                x => x.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .ReturnsAsync(mockConnector.Object);
            mockChannelServiceClientFactory.Setup(
                x => x.CreateUserTokenClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockUserTokenClient.Object);

            var adapter = new TestCloudAdapter(mockChannelServiceClientFactory.Object);

            // Arrange the Dialogs.
            var convoState = new ConversationState(new MemoryStorage());

            // Add the OAuthPrompt.
            var oauthPromptSettings = new OAuthPromptSettings
            {
                Text = "Please sign in",
                ConnectionName = connectionName,
                Title = "Sign in",
            };

            // The on-turn callback.
            DialogTurnResult dialogTurnResult = null;
            BotCallbackHandler callback = async (turnContext, cancellationToken) =>
            {
                var dialogState = await convoState.GetPropertyAsync<DialogState>(turnContext, "DialogState", () => new DialogState(), cancellationToken);
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", oauthPromptSettings));

                var dialogContext = await dialogs.CreateContextAsync(turnContext);
                dialogContext.Stack.Insert(
                    0,
                    new DialogInstance
                    {
                        Id = "OAuthPrompt",
                        State = new Dictionary<string, object>
                        {
                            { "expires", DateTime.UtcNow.AddHours(8) },
                            { "caller", null },
                            { "state", new Dictionary<string, object> { { "AttemptCount", 0 } } },
                            { "options", new PromptOptions() }
                        }
                    });

                dialogTurnResult = await dialogContext.ContinueDialogAsync();
            };

            // The Activity for the turn.

            var tokenResponse = new
            {
                channelId = channelId,
                connectionName = connectionName,
                token = "TOKEN",
                expiration = "expiration",
            };

            var activity = new Activity
            {
                Type = ActivityTypes.Event,
                Name = SignInConstants.TokenResponseEventName,
                From = new ChannelAccount { Id = userId },
                Conversation = new ConversationAccount { Id = "conversation-id" },
                ChannelId = channelId,
                Value = tokenResponse,
            };

            // Act
            var invokeResponse = await adapter.ProcessAsync(new ClaimsIdentity(), activity, callback);

            // Assert
            Assert.Equal(DialogTurnStatus.Complete, dialogTurnResult.Status);
            Assert.IsType<TokenResponse>(dialogTurnResult.Result);
            Assert.Equal("TOKEN", ((TokenResponse)dialogTurnResult.Result).Token);
        }

        [Fact]
        public async Task OAuthPromptContinueWithInvokeVerifyState()
        {
            // Arrange
            var userId = "user-id";
            var connectionName = "connection-name";
            var channelId = "channel-id";
            var magicCode = "123456";

            // Arrange the Adapter.
            var mockConnector = new Mock<IConnectorClient>();

            var mockUserTokenClient = new Mock<IUserTokenClient>();
            mockUserTokenClient.Setup(
                x => x.GetUserTokenAsync(It.Is<string>(s => s == userId), It.Is<string>(s => s == connectionName), It.Is<string>(s => s == channelId), It.Is<string>(s => s == magicCode), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenResponse { ChannelId = channelId, ConnectionName = connectionName, Token = $"TOKEN" });

            var mockChannelServiceClientFactory = new Mock<IChannelServiceClientFactory>();
            mockChannelServiceClientFactory.Setup(
                x => x.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .ReturnsAsync(mockConnector.Object);
            mockChannelServiceClientFactory.Setup(
                x => x.CreateUserTokenClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockUserTokenClient.Object);

            var adapter = new TestCloudAdapter(mockChannelServiceClientFactory.Object);

            // Arrange the Dialogs.
            var convoState = new ConversationState(new MemoryStorage());

            // Add the OAuthPrompt.
            var oauthPromptSettings = new OAuthPromptSettings
            {
                Text = "Please sign in",
                ConnectionName = connectionName,
                Title = "Sign in",
            };

            // The on-turn callback.
            DialogTurnResult dialogTurnResult = null;
            BotCallbackHandler callback = async (turnContext, cancellationToken) =>
            {
                var dialogState = await convoState.GetPropertyAsync<DialogState>(turnContext, "DialogState", () => new DialogState(), cancellationToken);
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", oauthPromptSettings));

                var dialogContext = await dialogs.CreateContextAsync(turnContext);
                dialogContext.Stack.Insert(
                    0,
                    new DialogInstance
                    {
                        Id = "OAuthPrompt",
                        State = new Dictionary<string, object>
                        {
                            { "expires", DateTime.UtcNow.AddHours(8) },
                            { "caller", null },
                            { "state", new Dictionary<string, object> { { "AttemptCount", 0 } } },
                            { "options", new PromptOptions() }
                        }
                    });

                dialogTurnResult = await dialogContext.ContinueDialogAsync();
            };

            // The Activity for the turn. 
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.VerifyStateOperationName,
                From = new ChannelAccount { Id = userId },
                Conversation = new ConversationAccount { Id = "conversation-id" },
                ChannelId = channelId,
                Value = new { state = magicCode },
            };

            // Act
            var invokeResponse = await adapter.ProcessAsync(new ClaimsIdentity(), activity, callback);

            // Assert
            Assert.Equal(DialogTurnStatus.Complete, dialogTurnResult.Status);
            Assert.IsType<TokenResponse>(dialogTurnResult.Result);
            Assert.Equal("TOKEN", ((TokenResponse)dialogTurnResult.Result).Token);
        }

        [Fact]
        public async Task OAuthPromptContinueWithInvokeTokenExchange()
        {
            // Arrange
            var userId = "user-id";
            var connectionName = "connection-name";
            var channelId = "channel-id";
            var tokenExchangeRequestToken = "123456";
            var tokenExchangeInvokeRequestId = "tokenExchangeInvokeRequest-id";

            // Arrange the Adapter.
            var mockConnector = new Mock<IConnectorClient>();

            var mockUserTokenClient = new Mock<IUserTokenClient>();
            mockUserTokenClient.Setup(
                x => x.ExchangeTokenAsync(It.Is<string>(s => s == userId), It.Is<string>(s => s == connectionName), It.Is<string>(s => s == channelId), It.Is<TokenExchangeRequest>(ter => ter.Token == tokenExchangeRequestToken), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenResponse { ChannelId = channelId, ConnectionName = connectionName, Token = $"TOKEN" });

            var mockChannelServiceClientFactory = new Mock<IChannelServiceClientFactory>();
            mockChannelServiceClientFactory.Setup(
                x => x.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .ReturnsAsync(mockConnector.Object);
            mockChannelServiceClientFactory.Setup(
                x => x.CreateUserTokenClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockUserTokenClient.Object);

            var adapter = new TestCloudAdapter(mockChannelServiceClientFactory.Object);

            // Arrange the Dialogs.
            var convoState = new ConversationState(new MemoryStorage());

            // Add the OAuthPrompt.
            var oauthPromptSettings = new OAuthPromptSettings
            {
                Text = "Please sign in",
                ConnectionName = connectionName,
                Title = "Sign in",
            };

            // The on-turn callback.
            DialogTurnResult dialogTurnResult = null;
            BotCallbackHandler callback = async (turnContext, cancellationToken) =>
            {
                var dialogState = await convoState.GetPropertyAsync<DialogState>(turnContext, "DialogState", () => new DialogState(), cancellationToken);
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", oauthPromptSettings));

                var dialogContext = await dialogs.CreateContextAsync(turnContext);
                dialogContext.Stack.Insert(
                    0,
                    new DialogInstance
                    {
                        Id = "OAuthPrompt",
                        State = new Dictionary<string, object>
                        {
                            { "expires", DateTime.UtcNow.AddHours(8) },
                            { "caller", null },
                            { "state", new Dictionary<string, object> { { "AttemptCount", 0 } } },
                            { "options", new PromptOptions() }
                        }
                    });

                dialogTurnResult = await dialogContext.ContinueDialogAsync();
            };

            // The Activity for the turn.

            var tokenExchangeInvokeRequest = new
            {
                id = tokenExchangeInvokeRequestId,
                connectionName = connectionName,
                token = tokenExchangeRequestToken,
            };

            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                From = new ChannelAccount { Id = userId },
                Conversation = new ConversationAccount { Id = "conversation-id" },
                ChannelId = channelId,
                Value = tokenExchangeInvokeRequest,
            };

            // Act
            var invokeResponse = await adapter.ProcessAsync(new ClaimsIdentity(), activity, callback);

            // Assert
            Assert.Equal(DialogTurnStatus.Complete, dialogTurnResult.Status);
            Assert.IsType<TokenResponse>(dialogTurnResult.Result);
            Assert.Equal("TOKEN", ((TokenResponse)dialogTurnResult.Result).Token);

            Assert.Equal(200, invokeResponse.Status);

            var tokenExchangeInvokeResponse = (TokenExchangeInvokeResponse)invokeResponse.Body;
            Assert.Equal(tokenExchangeInvokeRequestId, tokenExchangeInvokeResponse.Id);
            Assert.Equal(connectionName, tokenExchangeInvokeResponse.ConnectionName);
        }

        [Fact]
        public async Task OAuthPromptSignOutUser()
        {
            // Arrange
            var userId = "user-id";
            var connectionName = "connection-name";
            var channelId = "channel-id";

            // Arrange the Adapter.
            var mockConnector = new Mock<IConnectorClient>();

            var mockUserTokenClient = new Mock<IUserTokenClient>();
            mockUserTokenClient.Setup(
                x => x.SignOutUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()));

            var mockChannelServiceClientFactory = new Mock<IChannelServiceClientFactory>();
            mockChannelServiceClientFactory.Setup(
                x => x.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .ReturnsAsync(mockConnector.Object);
            mockChannelServiceClientFactory.Setup(
                x => x.CreateUserTokenClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockUserTokenClient.Object);

            var adapter = new TestCloudAdapter(mockChannelServiceClientFactory.Object);

            // Add the OAuthPrompt.
            var oauthPromptSettings = new OAuthPromptSettings
            {
                Text = "Please sign in",
                ConnectionName = connectionName,
                Title = "Sign in",
            };

            // The on-turn callback.
            BotCallbackHandler callback = async (turnContext, cancellationToken) =>
            {
                var oauthPrompt = new OAuthPrompt("OAuthPrompt", oauthPromptSettings);
                await oauthPrompt.SignOutUserAsync(turnContext, cancellationToken);
            };

            // The Activity for the turn.
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                From = new ChannelAccount { Id = userId },
                Conversation = new ConversationAccount { Id = "conversation-id" },
                ChannelId = channelId,
                Text = "logout",
            };

            // Act
            var invokeResponse = await adapter.ProcessAsync(new ClaimsIdentity(), activity, callback);

            // Assert
            mockUserTokenClient.Verify(
                x => x.SignOutUserAsync(It.Is<string>(s => s == userId), It.Is<string>(s => s == connectionName), It.Is<string>(s => s == channelId), It.IsAny<CancellationToken>()), Times.Once());
        }
    }
}

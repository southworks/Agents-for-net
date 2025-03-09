﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Microsoft.Agents.BotBuilder.App.UserAuth;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.BotBuilder.Tests.App.TestUtils;
using Microsoft.Agents.BotBuilder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.BotBuilder.Tests.App
{
    public class UserAuthenticationFeatureTests
    {
        private const string GraphName = "graph";
        private const string SharePointName = "sharepoint";
        private const string GraphToken = "graph token";
        private const string SharePointToken = "sharePoint token";
        private Mock<IUserAuthorization> MockGraph;
        private Mock<IUserAuthorization> MockSharePoint;
        private Mock<IChannelAdapter> MockChannelAdapter;

        public UserAuthenticationFeatureTests()
        {
            MockGraph = new Mock<IUserAuthorization>();
            MockGraph
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = GraphToken }));
            MockGraph
                .Setup(e => e.Name)
                .Returns(GraphName);

            MockSharePoint = new Mock<IUserAuthorization>();
            MockSharePoint
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = SharePointToken }));
            MockSharePoint
                .Setup(e => e.Name)
                .Returns(SharePointName);

            MockChannelAdapter = new Mock<IChannelAdapter>();
        }

        [Fact]
        public void Test_NoAdapter()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var app = new TestApplication(new TestApplicationOptions());
                var options = new UserAuthorizationOptions(MockGraph.Object, MockSharePoint.Object);
                var authManager = new TestUserAuthenticationFeature(app, options);
            });
        }

        [Fact]
        public async Task Test_AutoSignIn_Default()
        {
            // arrange
            var options = new TestApplicationOptions() 
            { 
                Adapter = MockChannelAdapter.Object, 
                UserAuthorization = new UserAuthorizationOptions(MockGraph.Object, MockSharePoint.Object) 
            };
            var app = new TestApplication(options);

            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            var response = await app.Authorization.AutoSignInUserAsync(turnContext, turnState);

            // assert
            Assert.True(response);
            Assert.NotNull(app.Authorization.GetToken(GraphName));
            Assert.Equal(GraphToken, app.Authorization.GetToken(GraphName));
        }

        [Fact]
        public async Task Test_AutoSignIn_Named()
        {
            // arrange
            var options = new TestApplicationOptions()
            {
                Adapter = MockChannelAdapter.Object,
                UserAuthorization = new UserAuthorizationOptions(MockGraph.Object, MockSharePoint.Object)
            };
            var app = new TestApplication(options);

            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            var signInComplete = await app.Authorization.AutoSignInUserAsync(turnContext, turnState, SharePointName);

            // assert
            Assert.True(signInComplete);
            Assert.NotNull(app.Authorization.GetToken(SharePointName));
            Assert.Equal(SharePointToken, app.Authorization.GetToken(SharePointName));
        }

        [Fact]
        public async Task Test_AutoSignIn_Pending()
        {
            MockGraph
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((TokenResponse) null));

            var options = new TestApplicationOptions()
            {
                Adapter = MockChannelAdapter.Object,
                UserAuthorization = new UserAuthorizationOptions(MockGraph.Object)
            };
            var app = new TestApplication(options);
            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            var signInComplete = await app.Authorization.AutoSignInUserAsync(turnContext, turnState);

            // assert
            Assert.False(signInComplete);
        }

        [Fact]
        public async Task Test_SignOut_DefaultHandler()
        {
            // arrange
            var options = new TestApplicationOptions()
            {
                Adapter = MockChannelAdapter.Object,
                UserAuthorization = new UserAuthorizationOptions(MockGraph.Object, MockSharePoint.Object)
            };
            var app = new TestApplication(options);
            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            await app.Authorization.AutoSignInUserAsync(turnContext, turnState);
            await app.Authorization.AutoSignInUserAsync(turnContext, turnState, SharePointName);
            await app.Authorization.SignOutUserAsync(turnContext, turnState);

            // assert
            Assert.Null(app.Authorization.GetToken(GraphName));
            Assert.NotNull(app.Authorization.GetToken(SharePointName));
        }

        [Fact]
        public async Task Test_SignOut_SpecificHandler()
        {
            // arrange
            var options = new TestApplicationOptions()
            {
                Adapter = MockChannelAdapter.Object,
                UserAuthorization = new UserAuthorizationOptions(MockGraph.Object, MockSharePoint.Object)
            };
            var app = new TestApplication(options);
            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            await app.Authorization.AutoSignInUserAsync(turnContext, turnState);
            await app.Authorization.AutoSignInUserAsync(turnContext, turnState, SharePointName);
            await app.Authorization.SignOutUserAsync(turnContext, turnState, SharePointName);

            // assert
            Assert.Null(app.Authorization.GetToken(SharePointName));
            Assert.NotNull(app.Authorization.GetToken(GraphName));
        }

        [Fact]
        public async Task Test_ManualSignInOnSuccessForCached()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // a IUserAuthorization that already has a token (user already signed in)
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = GraphToken }));
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // arrange
            var options = new TestApplicationOptions()
            {
                Adapter = adapter,
                TurnStateFactory = () => new TurnState(storage),
                UserAuthorization = new UserAuthorizationOptions(graphMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOff
                }
            };
            var app = new TestApplication(options);

            app.OnMessage("/signin", async (turnContext, turnState, cancellationToken) =>
            {
                await app.Authorization.SignInUserAsync(turnContext, turnState, GraphName);
            });

            app.Authorization.OnUserSignInSuccess(async (turnContext, turnState, handlerName, token, CancellationToken) =>
            {
                await turnContext.SendActivityAsync($"sign in success for {handlerName}", cancellationToken: CancellationToken.None);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("/signin")
            .AssertReply($"sign in success for {GraphName}")
            .StartTestAsync();

            // assert
            Assert.NotNull(app.Authorization.GetToken(GraphName));
        }

        [Fact]
        public async Task Test_ManualSignInOnSuccessForFlow()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // mock IUserAuthorization that returns null the first attempt, then a token after that.
            int attempt = 0;
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (attempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = GraphToken });
                });
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // arrange
            var options = new TestApplicationOptions()
            {
                Adapter = adapter,
                TurnStateFactory = () => new TurnState(storage),
                UserAuthorization = new UserAuthorizationOptions(graphMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOff
                }
            };
            var app = new TestApplication(options);

            app.OnMessage("/signin", async (turnContext, turnState, cancellationToken) =>
            {
                await app.Authorization.SignInUserAsync(turnContext, turnState, GraphName);
            }); 

            app.Authorization.OnUserSignInSuccess(async (turnContext, turnState, handlerName, token, CancellationToken) =>
            {
                await turnContext.SendActivityAsync($"sign in success for {handlerName}", cancellationToken: CancellationToken.None);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("/signin")
            .Send("magic code")
            .AssertReply($"sign in success for {GraphName}")
            .StartTestAsync();

            // assert
            Assert.NotNull(app.Authorization.GetToken(GraphName));
        }

        [Fact]
        public async Task Test_AutoSignInForCached()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = GraphToken }));
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // arrange
            var options = new TestApplicationOptions()
            {
                Adapter = adapter,
                TurnStateFactory = () => new TurnState(storage),
                UserAuthorization = new UserAuthorizationOptions(graphMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOn
                }
            };
            var app = new TestApplication(options);

            app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("first message")
            .AssertReply("You said: first message")
            .StartTestAsync();

            // assert
            Assert.NotNull(app.Authorization.GetToken(GraphName));
        }

        [Fact]
        public async Task Test_AutoSignInForFlow()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // mock IUserAuthorization that returns null the first attempt, then a token after that.
            int attempt = 0;
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (attempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = GraphToken });
                });
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // arrange
            var options = new TestApplicationOptions()
            {
                Adapter = adapter,
                TurnStateFactory = () => new TurnState(storage),
                UserAuthorization = new UserAuthorizationOptions(graphMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOn
                }
            };
            var app = new TestApplication(options);

            app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("first message")
            .Send("magic code")
            .AssertReply("You said: first message")
            .StartTestAsync();

            // assert
            Assert.NotNull(app.Authorization.GetToken(GraphName));
        }

        private static TurnContext MockTurnContext()
        {
            return new TurnContext(new SimpleAdapter(), new Activity()
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder.Testing;
using Moq;
using Xunit;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Client;
using Microsoft.Agents.Storage;
using System.Text.Json;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Connector;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.BotBuilder.Compat;
using Microsoft.Agents.Authentication;
using System.Security.Claims;
using Microsoft.Agents.Connector.Types;
using System.Net;

namespace Microsoft.Agents.BotBuilder.Dialogs.Tests
{
    public class SkillDialogTests
    {
        static readonly IStorage _storage = new MemoryStorage();
        static readonly HttpBotChannelSettings _httpBotChannelSettings;
        static readonly Mock<IAccessTokenProvider> _accessTokenProvider;
        static readonly IChannelHost _channelHost;
        static readonly string _hostAppId = Guid.NewGuid().ToString();
        static readonly Mock<IConnections> _connections;
        static readonly Mock<IHttpClientFactory> _httpFactory;
        static HttpResponseMessage _httpResponse;
        static readonly Mock<HttpClient> _httpClient;
        static SkillDialogTests()
        {
            _accessTokenProvider = new Mock<IAccessTokenProvider>();
            _accessTokenProvider
                .Setup(p => p.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<String>>(), It.IsAny<bool>()))
                .Returns(Task.FromResult("token"));

            _connections = new Mock<IConnections>();
            _connections
                .Setup(c => c.GetConnection(It.IsAny<string>()))
                .Returns(_accessTokenProvider.Object);

            IAccessTokenProvider provider = _accessTokenProvider.Object;
            _connections
                .Setup(c => c.TryGetConnection(It.IsAny<string>(), out provider))
                .Returns(true);

            _httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
            _httpClient = new Mock<HttpClient>();
            _httpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(_httpResponse);

            _httpFactory = new Mock<IHttpClientFactory>();
            _httpFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient.Object);

            _httpBotChannelSettings = new HttpBotChannelSettings() { Alias = "test" };
            _httpBotChannelSettings.ConnectionSettings.ClientId = Guid.NewGuid().ToString();
            _httpBotChannelSettings.ConnectionSettings.Endpoint = new Uri("http://testskill.contoso.com/api/messages");
            _httpBotChannelSettings.ConnectionSettings.TokenProvider = "BotServiceConnection";

            _channelHost = new ConfigurationChannelHost(
                new Mock<IServiceProvider>().Object,
                _storage,
                _connections.Object,
                _httpFactory.Object,
                new Dictionary<string, HttpBotChannelSettings> { { "test", _httpBotChannelSettings } },
                "https://localhost",
                _hostAppId);
        }

        private readonly Mock<ITurnContext> _context = new();

        private readonly DialogState _dialogState = new([
            new DialogInstance {
                Id = "A",
                State = new Dictionary<string, object> {
                    { "deliverymode", DeliveryModes.ExpectReplies},
                    { "Microsoft.Agents.BotBuilder.Dialogs.SkillDialog.SkillConversationId", "conversationId"}
                }
            }
        ]);
        private readonly Mock<DialogContext> _dialogContext;
        private readonly MockSkillDialog _dialog;

        private readonly ClaimsIdentity _claimsIdentity = new ClaimsIdentity(
            [
                new (AuthenticationConstants.AudienceClaim, _hostAppId),
                new (AuthenticationConstants.AppIdClaim, _hostAppId),
            ]);

        public SkillDialogTests()
        {
            _dialogContext = new(new DialogSet(), _context.Object, _dialogState);
            _dialog = new(new SkillDialogOptions()
            {
                Skill = "test",
                ConversationState = new ConversationState(new MemoryStorage()),
                ChannelHost = _channelHost
            });
        }

        [Fact]
        public void ConstructorValidationTests()
        {
            Assert.Throws<ArgumentNullException>(() => { new SkillDialog(null); });
        }

        [Fact]
        public async Task BeginDialogOptionsValidation()
        {
            var dialogOptions = new SkillDialogOptions();
            var sut = new SkillDialog(dialogOptions);
            var client = new DialogTestClient(Channels.Test, sut);
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.SendActivityAsync<Activity>("irrelevant"));

            client = new DialogTestClient(Channels.Test, sut, new Dictionary<string, string>());
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.SendActivityAsync<Activity>("irrelevant"));

            client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions());
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.SendActivityAsync<Activity>("irrelevant"));
        }

        [Theory(Skip = "Need full IChannetHost.SendToChannel Mock")]
        [InlineData(null)]
        [InlineData(DeliveryModes.ExpectReplies)]
        public async Task BeginDialogCallsSkill(string deliveryMode)
        {
            IActivity activitySent = null;
            string toConversationId = null;

            // Callback to capture the parameters sent to the skill
            void CaptureAction(string conversationId, IActivity activity, IActivity relatesTo, CancellationToken cancellationToken)
            {
                // Capture values sent to the skill so we can assert the right parameters were used.
                toConversationId = conversationId;
                activitySent = activity;
            }

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClient = CreateMockSkillClient(CaptureAction);

            // Use Memory for conversation state
            var conversationState = new ConversationState(new MemoryStorage());
            var dialogOptions = CreateSkillDialogOptions(_channelHost, conversationState, mockSkillClient);

            // Create the SkillDialogInstance and the activity to send.
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.DeliveryMode = deliveryMode;
            activityToSend.Text = Guid.NewGuid().ToString();
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);

            //!!! Assert.Equal(0, ((SimpleConversationIdFactory)dialogOptions.ConversationIdFactory).CreateCount);

            // Send something to the dialog to start it
            await client.SendActivityAsync<Activity>("irrelevant");

            // Assert results and data sent to the SkillClient for fist turn
            //!!! Assert.Equal(1, ((SimpleConversationIdFactory)dialogOptions.ConversationIdFactory).CreateCount);
            Assert.Equal(activityToSend.Text, activitySent.Text);
            Assert.Equal(DialogTurnStatus.Waiting, client.DialogTurnResult.Status);

            // Send a second message to continue the dialog
            await client.SendActivityAsync<Activity>("Second message");
            //!!! Assert.Equal(1, ((SimpleConversationIdFactory)dialogOptions.ConversationIdFactory).CreateCount);

            // Assert results for second turn
            Assert.Equal("Second message", activitySent.Text);
            Assert.Equal(DialogTurnStatus.Waiting, client.DialogTurnResult.Status);

            // Send EndOfConversation to the dialog
            await client.SendActivityAsync<Activity>((Activity)Activity.CreateEndOfConversationActivity());

            // Assert we are done.
            Assert.Equal(DialogTurnStatus.Complete, client.DialogTurnResult.Status);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ShouldHandleInvokeActivities()
        {
            IActivity activitySent = null;
            string toConversationId = null;

            // Callback to capture the parameters sent to the skill
            void CaptureAction(string conversationId, IActivity activity, IActivity relatesTo, CancellationToken cancellationToken)
            {
                // Capture values sent to the skill so we can assert the right parameters were used.
                toConversationId = conversationId;
                activitySent = activity;
            }

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClient = CreateMockSkillClient(CaptureAction);

            // Use Memory for conversation state
            var conversationState = new ConversationState(new MemoryStorage());
            var dialogOptions = CreateSkillDialogOptions(_channelHost, conversationState, mockSkillClient);

            // Create the SkillDialogInstance and the activity to send.
            var activityToSend = Activity.CreateInvokeActivity();
            activityToSend.Name = Guid.NewGuid().ToString();
            var sut = new SkillDialog(dialogOptions);
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);

            // Send something to the dialog to start it
            await client.SendActivityAsync<Activity>("irrelevant");

            // Assert results and data sent to the SkillClient for fist turn
            Assert.Equal(activityToSend.Name, activitySent.Name);
            Assert.Equal(DeliveryModes.ExpectReplies, activitySent.DeliveryMode);
            Assert.Equal(DialogTurnStatus.Waiting, client.DialogTurnResult.Status);

            // Send a second message to continue the dialog
            await client.SendActivityAsync<Activity>("Second message");

            // Assert results for second turn
            Assert.Equal("Second message", activitySent.Text);
            Assert.Equal(DialogTurnStatus.Waiting, client.DialogTurnResult.Status);

            // Send EndOfConversation to the dialog
            await client.SendActivityAsync<Activity>((Activity)Activity.CreateEndOfConversationActivity());

            // Assert we are done.
            Assert.Equal(DialogTurnStatus.Complete, client.DialogTurnResult.Status);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task CancelDialogSendsEoC()
        {
            IActivity activitySent = null;

            // Callback to capture the parameters sent to the skill
            void CaptureAction(string conversationId, IActivity activity, IActivity relatesTo, CancellationToken cancellationToken)
            {
                // Capture values sent to the skill so we can assert the right parameters were used.
                activitySent = activity;
            }

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClient = CreateMockSkillClient(CaptureAction);

            // Use Memory for conversation state
            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_channelHost, conversationState, mockSkillClient);

            // Create the SkillDialogInstance and the activity to send.
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.Text = Guid.NewGuid().ToString();
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);

            // Send something to the dialog to start it
            await client.SendActivityAsync<Activity>("irrelevant");

            // Cancel the dialog so it sends an EoC to the skill
            await client.DialogContext.CancelAllDialogsAsync(CancellationToken.None);

            Assert.Equal(ActivityTypes.EndOfConversation, activitySent.Type);
        }

        [Fact]
        public async Task ShouldThrowHttpExceptionOnPostFailure()
        {
            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClient = CreateMockSkillClient(null, 500);

            // Use Memory for conversation state
            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_channelHost, conversationState, mockSkillClient);

            // Create the SkillDialogInstance and the activity to send.
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.Text = Guid.NewGuid().ToString();
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);

            // Send something to the dialog 
            await Assert.ThrowsAsync<HttpRequestException>(async () => await client.SendActivityAsync<Activity>("irrelevant"));
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ShouldInterceptOAuthCardsForSso()
        {
            var connectionName = "connectionName";
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IChannel>();
            mockSkillClient
                .SetupSequence(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies> { Status = 200 }));

            var mockUserTokenClient = new Mock<IUserTokenClient>();
            mockUserTokenClient
                .SetupSequence(x => x.ExchangeTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TokenExchangeRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse()
                {
                    ChannelId = Channels.Test,
                    ConnectionName = connectionName,
                    Token = "https://test1"
                }));

            var conversationState = new ConversationState(_storage);
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));

            var dialogOptions = CreateSkillDialogOptions(_channelHost, conversationState, mockSkillClient, connectionName);
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var client = new DialogTestClient(testAdapter, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);
            testAdapter.AddExchangeableToken(connectionName, Channels.Test, "user1", "https://test", "https://test1");
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.Null(finalActivity);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ShouldNotInterceptOAuthCardsForEmptyConnectionName()
        {
            var connectionName = "connectionName";
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IChannel>();
            mockSkillClient
                .Setup(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }));

            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_channelHost, conversationState, mockSkillClient);

            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));
            var client = new DialogTestClient(testAdapter, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);
            testAdapter.AddExchangeableToken(connectionName, Channels.Test, "user1", "https://test", "https://test1");
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.NotNull(finalActivity);
            Assert.Single(finalActivity.Attachments);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ShouldNotInterceptOAuthCardsForEmptyToken()
        {
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IChannel>();
            mockSkillClient
                .Setup(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }));

            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_channelHost, conversationState, mockSkillClient);

            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));
            var client = new DialogTestClient(testAdapter, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);

            // Don't add exchangeable token to test adapter
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.NotNull(finalActivity);
            Assert.Single(finalActivity.Attachments);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ShouldNotInterceptOAuthCardsForTokenException()
        {
            var connectionName = "connectionName";
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IChannel>();
            mockSkillClient
                .Setup(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }));

            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_channelHost, conversationState, mockSkillClient, connectionName);

            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));
            var initialDialogOptions = new BeginSkillDialogOptions { Activity = activityToSend };
            var client = new DialogTestClient(testAdapter, sut, initialDialogOptions, conversationState: conversationState, contextClaims: _claimsIdentity);
            testAdapter.ThrowOnExchangeRequest(connectionName, Channels.Test, "user1", "https://test");
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.NotNull(finalActivity);
            Assert.Single(finalActivity.Attachments);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ShouldNotInterceptOAuthCardsForBadRequest()
        {
            var connectionName = "connectionName";
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IChannel>();
            mockSkillClient
                .SetupSequence(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies> { Status = 409 }));

            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_channelHost, conversationState, mockSkillClient, connectionName);

            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));
            var client = new DialogTestClient(testAdapter, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);
            testAdapter.AddExchangeableToken(connectionName, Channels.Test, "user1", "https://test", "https://test1");
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.NotNull(finalActivity);
            Assert.Single(finalActivity.Attachments);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task EndOfConversationFromExpectRepliesCallsDeleteConversationReferenceAsync()
        {
            IActivity activitySent = null;

            // Callback to capture the parameters sent to the skill
            void CaptureAction(string conversationId, IActivity activity, IActivity relatesTo, CancellationToken cancellationToken)
            {
                // Capture values sent to the skill so we can assert the right parameters were used.
                activitySent = activity;
            }

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClientx = CreateMockSkillClient(CaptureAction);

            var eoc = Activity.CreateEndOfConversationActivity() as Activity;
            var expectedReplies = new List<IActivity>();
            expectedReplies.Add(eoc);

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClient = CreateMockSkillClient(CaptureAction, expectedReplies: expectedReplies);

            // Use Memory for conversation state
            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_channelHost, conversationState, mockSkillClient);

            // Create the SkillDialogInstance and the activity to send.
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.DeliveryMode = DeliveryModes.ExpectReplies;
            activityToSend.Text = Guid.NewGuid().ToString();
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);

            // Send something to the dialog to start it
            await client.SendActivityAsync<Activity>("hello");

            //!!! Assert.Empty((dialogOptions.ConversationIdFactory as SimpleConversationIdFactory).ConversationRefs);
            //!!! Assert.Equal(1, (dialogOptions.ConversationIdFactory as SimpleConversationIdFactory).CreateCount);
        }

        [Fact]
        public async Task ContinueDialogAsync_ShouldReturnEndOfTurnOnValidateActivity()
        {
            _context.SetupGet(e => e.Activity)
                .Returns(new Activity { Text = "shouldNotValidate" })
                .Verifiable(Times.Exactly(1));

            var result = await _dialog.ContinueDialogAsync(_dialogContext.Object);

            Assert.Equal(Dialog.EndOfTurn, result);
            Mock.Verify(_context);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ContinueDialogAsync_ShouldSendInvokeActivity()
        {
            var resultInvoke = new InvokeResponse { Status = 200, Body = "testing" };
            var activity = new Activity { Type = ActivityTypes.InvokeResponse, Value = JsonSerializer.Serialize(resultInvoke) };

            _context.SetupGet(e => e.Activity)
                .Returns(activity)
                .Verifiable(Times.Exactly(3));
            _context.SetupGet(e => e.Services)
                .Returns([])
                .Verifiable(Times.Exactly(1));
            _context.SetupGet(e => e.StackState)
                .Returns([]);

            MockSendToSkillAsync(activity);

            var result = await _dialog.ContinueDialogAsync(_dialogContext.Object);

            Assert.Equal(DialogTurnStatus.Waiting, result.Status);
            Assert.Equal(resultInvoke.Status, (activity.Value as InvokeResponse).Status);
            Assert.Equal(resultInvoke.Body, (activity.Value as InvokeResponse).Body.ToString());
            Mock.Verify(_context);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ContinueDialogAsync_ShouldReturnEndOfDialog()
        {
            var activity = new Activity { Type = ActivityTypes.EndOfConversation, Value = "EOC testing" };

            _context.SetupGet(e => e.Activity)
                .Returns(new Activity())
                .Verifiable(Times.Exactly(3));
            _context.SetupGet(e => e.Services)
                .Returns([]);
            _context.SetupGet(e => e.StackState)
                .Returns([]);
            MockSendToSkillAsync(activity);

            var result = await _dialog.ContinueDialogAsync(_dialogContext.Object);

            Assert.Equal(DialogTurnStatus.Complete, result.Status);
            Assert.Equal(activity.Value, result.Result);
            Mock.Verify(_context);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task RepromptDialogAsync_ShouldSendActivity()
        {
            var activity = new Activity();

            _context.SetupGet(e => e.Activity)
                .Returns(activity)
                .Verifiable(Times.Once);
            _context.SetupGet(e => e.Services)
                .Returns([]);
            _context.SetupGet(e => e.StackState)
                .Returns([]);
            MockSendToSkillAsync(activity);

            await _dialog.RepromptDialogAsync(_context.Object, _dialogState.DialogStack[0]);

            Mock.Verify(_context);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ResumeDialogAsync_ShouldSendActivity()
        {
            var activity = new Activity();

            _context.SetupGet(e => e.Activity)
                .Returns(activity)
                .Verifiable(Times.Once);
            _context.SetupGet(e => e.Services)
                .Returns([]);
            _context.SetupGet(e => e.StackState)
                .Returns([]);
            MockSendToSkillAsync(activity);

            var result = await _dialog.ResumeDialogAsync(_dialogContext.Object, DialogReason.BeginCalled);

            Assert.Equal(DialogTurnStatus.Waiting, result.Status);
            Mock.Verify(_context);
        }

        private static IActivity CreateOAuthCardAttachmentActivity(string uri)
        {
            var oauthCard = new OAuthCard { TokenExchangeResource = new TokenExchangeResource { Uri = uri } };
            var attachment = new Attachment
            {
                ContentType = OAuthCard.ContentType,
                Content = JsonSerializer.SerializeToNode(oauthCard, ProtocolJsonSerializer.SerializationOptions)
            };

            var attachmentActivity = MessageFactory.Attachment(attachment);
            attachmentActivity.Conversation = new ConversationAccount { Id = Guid.NewGuid().ToString() };
            attachmentActivity.From = new ChannelAccount("blah", "name");

            return attachmentActivity;
        }

        /// <summary>
        /// Helper to create a <see cref="SkillDialogOptions"/> for the skillDialog.
        /// </summary>
        /// <param name="conversationState"> The conversation state object.</param>
        /// <param name="mockSkillClient"> The skill client mock.</param>
        /// <returns> A Skill Dialog Options object.</returns>
        private static SkillDialogOptions CreateSkillDialogOptions(IChannelHost channelHost, ConversationState conversationState, Mock<IChannel> mockSkillClient, string connectionName = null)
        {
            var dialogOptions = new SkillDialogOptions
            {
                ChannelHost = channelHost,
                Skill = "test",
                ConversationState = conversationState,
                ConnectionName = connectionName
            };
            return dialogOptions;
        }

        private static Mock<IChannel> CreateMockSkillClient(Action<string, IActivity, IActivity,CancellationToken> captureAction, int returnStatus = 200, IList<IActivity> expectedReplies = null)
        {
            var mockSkillClient = new Mock<IChannel>();
            var activityList = new ExpectedReplies(expectedReplies ?? new List<IActivity> { MessageFactory.Text("dummy activity") });

            if (captureAction != null)
            {
                mockSkillClient
                    .Setup(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                    {
                        Status = returnStatus,
                        Body = activityList
                    }))
                    .Callback(captureAction);
            }
            else
            {
                mockSkillClient
                    .Setup(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                    {
                        Status = returnStatus,
                        Body = activityList
                    }));
            }

            return mockSkillClient;
        }

        private Activity CreateSendActivity()
        {
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.DeliveryMode = DeliveryModes.ExpectReplies;
            activityToSend.Text = Guid.NewGuid().ToString();
            return activityToSend;
        }

        // Simple conversation ID factory for testing.
        private class SimpleConversationIdFactory : IConversationIdFactory
        {
            public SimpleConversationIdFactory()
            {
                ConversationRefs = new ConcurrentDictionary<string, BotConversationReference>();
            }

            public ConcurrentDictionary<string, BotConversationReference> ConversationRefs { get; private set; }

            // Helper property to assert how many times is CreateSkillConversationIdAsync called.
            public int CreateCount { get; private set; }

            public Task<string> CreateConversationIdAsync(ConversationIdFactoryOptions options, CancellationToken cancellationToken)
            {
                CreateCount++;

                var key = (options.Activity.Conversation.Id + options.Activity.ServiceUrl).GetHashCode().ToString(CultureInfo.InvariantCulture);
                ConversationRefs.GetOrAdd(key, new BotConversationReference
                {
                    ConversationReference = options.Activity.GetConversationReference(),
                    OAuthScope = options.FromBotOAuthScope
                });
                return Task.FromResult(key);
            }

            public Task<BotConversationReference> GetBotConversationReferenceAsync(string skillConversationId, CancellationToken cancellationToken)
            {
                return Task.FromResult(ConversationRefs[skillConversationId]);
            }

            public Task DeleteConversationReferenceAsync(string skillConversationId, CancellationToken cancellationToken)
            {
                ConversationRefs.TryRemove(skillConversationId, out _);
                return Task.CompletedTask;
            }
        }

        private class MockSkillDialog(SkillDialogOptions dialogOptions, string dialogId = null) : SkillDialog(dialogOptions, dialogId)
        {
            protected override bool OnValidateActivity(IActivity activity)
            {
                return !(activity.Text == "shouldNotValidate");
            }
        }
        private void MockSendToSkillAsync(Activity activity)
        {
            var invokeResponse = new InvokeResponse<ExpectedReplies> { Status = 200, Body = new ExpectedReplies([activity]) };

            _context.Setup(e => e.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse())
                .Verifiable(Times.AtMostOnce);
        }
    }
}

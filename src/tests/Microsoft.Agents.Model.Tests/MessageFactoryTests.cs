// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Adapters;
using Microsoft.Agents.Protocols.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class MessageFactoryTests
    {
        [Fact]
        public void Text_ShouldCreateMessageActivityWithNullText()
        {
            var message = MessageFactory.Text(null);

            Assert.Null(message.Text);
            Assert.Equal(ActivityTypes.Message, message.Type);
        }

        [Fact]
        public void Text_ShouldCreateMessageActivityWithTextOnly()
        {
            var messageText = Guid.NewGuid().ToString();

            var message = MessageFactory.Text(messageText);

            Assert.Equal(message.Text, messageText);
            Assert.Equal(ActivityTypes.Message, message.Type);
        }

        [Fact]
        public void Text_ShouldCreateMessageActivityWithTextAndSSML()
        {
            var messageText = Guid.NewGuid().ToString();
            var ssml = @"
                <speak xmlns=""http://www.w3.org/2001/10/synthesis""
                       xmlns:dc=""http://purl.org/dc/elements/1.1/""
                       version=""1.0"">
                  <p>
                    <s xml:lang=""en-US"">
                      <voice name=""Bot"" gender=""neutral"" age=""2"">
                        Bots are <emphasis>Awesome</emphasis>.
                      </voice>
                    </s>
                  </p>
                </speak>";

            var message = MessageFactory.Text(messageText, ssml);

            Assert.Equal(message.Text, messageText);
            Assert.Equal(message.Speak, ssml);
            Assert.Equal(InputHints.AcceptingInput, message.InputHint);
            Assert.Equal(ActivityTypes.Message, message.Type);
        }

        [Fact]
        public void SuggestedActions_ShouldCreateMessageWithTextActions()
        {
            var text = Guid.NewGuid().ToString();
            var ssml = Guid.NewGuid().ToString();
            var inputHint = InputHints.ExpectingInput;
            var textActions = new List<string> { "one", "two" };

            var message = MessageFactory.SuggestedActions(textActions, text, ssml, inputHint);

            Assert.Equal(message.Text, text);
            Assert.Equal(ActivityTypes.Message, message.Type);
            Assert.Equal(message.InputHint, inputHint);
            Assert.Equal(message.Speak, ssml);
            Assert.NotNull(message.SuggestedActions);
            Assert.NotNull(message.SuggestedActions.Actions);
            Assert.Equal(2, message.SuggestedActions.Actions.Count);
            Assert.Equal("one", (string)message.SuggestedActions.Actions[0].Value);
            Assert.Equal("one", message.SuggestedActions.Actions[0].Title);
            Assert.True(message.SuggestedActions.Actions[0].Type == ActionTypes.ImBack);
            Assert.Equal("two", (string)message.SuggestedActions.Actions[1].Value);
            Assert.Equal("two", message.SuggestedActions.Actions[1].Title);
            Assert.True(message.SuggestedActions.Actions[1].Type == ActionTypes.ImBack);
        }

        [Fact]
        public void SuggestedActions_ShouldCreateMessageWithEnumerableActions()
        {
            var text = Guid.NewGuid().ToString();
            var ssml = Guid.NewGuid().ToString();
            var inputHint = InputHints.ExpectingInput;
            var textActions = new HashSet<string> { "one", "two", "three" };

            var message = MessageFactory.SuggestedActions(textActions, text, ssml, inputHint);

            Assert.Equal(message.Text, text);
            Assert.Equal(ActivityTypes.Message, message.Type);
            Assert.Equal(message.InputHint, inputHint);
            Assert.Equal(message.Speak, ssml);
            Assert.NotNull(message.SuggestedActions);
            Assert.NotNull(message.SuggestedActions.Actions);
            Assert.True(
                textActions.SetEquals(message.SuggestedActions.Actions.Select(action => (string)action.Value)),
                "The message's suggested actions have the wrong set of values.");
            Assert.True(
                textActions.SetEquals(message.SuggestedActions.Actions.Select(action => action.Title)),
                "The message's suggested actions have the wrong set of titles.");
            Assert.True(
                message.SuggestedActions.Actions.All(action => action.Type.Equals(ActionTypes.ImBack, StringComparison.Ordinal)),
                "The message's suggested actions are of the wrong action type.");
        }

        [Fact]
        public void SuggestedActions_ShouldCreateMessageWithCardAction()
        {
            var text = Guid.NewGuid().ToString();
            var ssml = Guid.NewGuid().ToString();
            var inputHint = InputHints.ExpectingInput;

            var cardActionValue = Guid.NewGuid().ToString();
            var cardActionTitle = Guid.NewGuid().ToString();

            var card = new CardAction
            {
                Type = ActionTypes.ImBack,
                Value = cardActionValue,
                Title = cardActionTitle,
            };

            var cardActions = new List<CardAction> { card };

            var message = MessageFactory.SuggestedActions(cardActions, text, ssml, inputHint);

            Assert.Equal(message.Text, text);
            Assert.Equal(ActivityTypes.Message, message.Type);
            Assert.Equal(message.InputHint, inputHint);
            Assert.Equal(message.Speak, ssml);
            Assert.NotNull(message.SuggestedActions);
            Assert.NotNull(message.SuggestedActions.Actions);
            Assert.Single(message.SuggestedActions.Actions);
            Assert.True((string)message.SuggestedActions.Actions[0].Value == cardActionValue);
            Assert.True(message.SuggestedActions.Actions[0].Title == cardActionTitle);
            Assert.True(message.SuggestedActions.Actions[0].Type == ActionTypes.ImBack);
        }

        [Fact]
        public void SuggestedActions_ShouldCreateMessageWithCardActionUnordered()
        {
            var text = Guid.NewGuid().ToString();
            var ssml = Guid.NewGuid().ToString();
            var inputHint = InputHints.ExpectingInput;

            var cardValue1 = Guid.NewGuid().ToString();
            var cardTitle1 = Guid.NewGuid().ToString();

            var cardAction1 = new CardAction
            {
                Type = ActionTypes.ImBack,
                Value = cardValue1,
                Title = cardTitle1,
            };

            var cardValue2 = Guid.NewGuid().ToString();
            var cardTitle2 = Guid.NewGuid().ToString();

            var cardAction2 = new CardAction
            {
                Type = ActionTypes.ImBack,
                Value = cardValue2,
                Title = cardTitle2,
            };

            var cardActions = new HashSet<CardAction> { cardAction1, cardAction2 };
            var values = new HashSet<object> { cardValue1, cardValue2 };
            var titles = new HashSet<string> { cardTitle1, cardTitle2 };

            var message = MessageFactory.SuggestedActions(cardActions, text, ssml, inputHint);

            Assert.Equal(message.Text, text);
            Assert.Equal(ActivityTypes.Message, message.Type);
            Assert.Equal(message.InputHint, inputHint);
            Assert.Equal(message.Speak, ssml);
            Assert.NotNull(message.SuggestedActions);
            Assert.NotNull(message.SuggestedActions.Actions);
            Assert.Equal(2, message.SuggestedActions.Actions.Count);
            Assert.True(
                values.SetEquals(message.SuggestedActions.Actions.Select(action => action.Value)),
                "The message's suggested actions have the wrong set of values.");
            Assert.True(
                titles.SetEquals(message.SuggestedActions.Actions.Select(action => action.Title)),
                "The message's suggested actions have the wrong set of titles.");
            Assert.True(
                message.SuggestedActions.Actions.All(action => action.Type.Equals(ActionTypes.ImBack, StringComparison.Ordinal)),
                "The message's suggested actions are of the wrong action type.");
        }

        [Fact]
        public void Attachment_ShouldCreateMessageWithSingleAttachment()
        {
            var text = Guid.NewGuid().ToString();
            var ssml = Guid.NewGuid().ToString();
            var inputHint = InputHints.ExpectingInput;

            var attachmentName = Guid.NewGuid().ToString();
            var attachment = new Attachment
            {
                Name = attachmentName,
            };

            var message = MessageFactory.Attachment(attachment, text, ssml, inputHint);

            Assert.Equal(message.Text, text);
            Assert.Equal(ActivityTypes.Message, message.Type);
            Assert.Equal(message.InputHint, inputHint);
            Assert.Equal(message.Speak, ssml);
            Assert.True(message.Attachments.Count == 1, "Incorrect Attachment Count");
            Assert.True(message.Attachments[0].Name == attachmentName, "Incorrect Attachment Name");
        }

        [Fact]
        public void SuggestedActions_ThrowsOnCardActionNullCollection()
        {
            Assert.Throws<ArgumentNullException>(() => MessageFactory.SuggestedActions((IEnumerable<CardAction>)null, null, null, null));
        }

        [Fact]
        public void SuggestedActions_ThrowsOnStringNullCollection()
        {
            Assert.Throws<ArgumentNullException>(() => MessageFactory.SuggestedActions((IEnumerable<string>)null, null, null, null));
        }

        [Fact]
        public void Attachment_ThrowsOnNullAttachment()
        {
            Assert.Throws<ArgumentNullException>(() => MessageFactory.Attachment((Attachment)null));
        }

        [Fact]
        public void Attachment_ThrowsOnMultipleNullAttachments()
        {
            Assert.Throws<ArgumentNullException>(() => MessageFactory.Attachment((IList<Attachment>)null));
        }

        [Fact]
        public void Carousel_ThrowsOnNullAttachments()
        {
            Assert.Throws<ArgumentNullException>(() => MessageFactory.Carousel((IList<Attachment>)null));
        }

        [Theory]
        [InlineData("url", null)]
        [InlineData("url", "")]
        [InlineData("url", " ")]
        [InlineData(null, "contentType")]
        [InlineData("", "contentType")]
        [InlineData(" ", "contentType")]
        public void ContentUrl_ThrowsOnInvalidArguments(string url, string contentType)
        {
            Assert.Throws<ArgumentNullException>(() => MessageFactory.ContentUrl(url, contentType));
        }

        [Fact]
        public void Carousel_ShouldCreateMessageWithTwoAttachments()
        {
            var text = Guid.NewGuid().ToString();
            var ssml = Guid.NewGuid().ToString();
            var inputHint = InputHints.ExpectingInput;

            var attachmentName = Guid.NewGuid().ToString();
            var attachment1 = new Attachment
            {
                Name = attachmentName,
            };

            var attachmentName2 = Guid.NewGuid().ToString();
            var attachment2 = new Attachment
            {
                Name = attachmentName2,
            };

            var multipleAttachments = new List<Attachment> { attachment1, attachment2 };

            var message = MessageFactory.Carousel(multipleAttachments, text, ssml, inputHint);

            Assert.Equal(message.Text, text);
            Assert.Equal(ActivityTypes.Message, message.Type);
            Assert.Equal(message.InputHint, inputHint);
            Assert.Equal(message.Speak, ssml);
            Assert.True(message.AttachmentLayout == AttachmentLayoutTypes.Carousel);
            Assert.True(message.Attachments.Count == 2, "Incorrect Attachment Count");
            Assert.True(message.Attachments[0].Name == attachmentName, "Incorrect Attachment1 Name");
            Assert.True(message.Attachments[1].Name == attachmentName2, "Incorrect Attachment2 Name");
        }

        [Fact]
        public void Carousel_ShouldCreateMessageWithlUnorderedAttachments()
        {
            var text = Guid.NewGuid().ToString();
            var ssml = Guid.NewGuid().ToString();
            var inputHint = InputHints.ExpectingInput;

            var attachmentName1 = Guid.NewGuid().ToString();
            var attachment1 = new Attachment
            {
                Name = attachmentName1,
            };

            var attachmentName2 = Guid.NewGuid().ToString();
            var attachment2 = new Attachment
            {
                Name = attachmentName2,
            };

            var multipleAttachments = new HashSet<Attachment> { attachment1, attachment2 };
            var names = new HashSet<string> { attachmentName1, attachmentName2 };

            var message = MessageFactory.Carousel(multipleAttachments, text, ssml, inputHint);

            Assert.Equal(message.Text, text);
            Assert.Equal(ActivityTypes.Message, message.Type);
            Assert.Equal(message.InputHint, inputHint);
            Assert.Equal(message.Speak, ssml);
            Assert.True(message.AttachmentLayout == AttachmentLayoutTypes.Carousel);
            Assert.True(message.Attachments.Count == 2, "Incorrect Attachment Count");
            Assert.True(names.SetEquals(message.Attachments.Select(a => a.Name)), "Incorrect set of attachment names.");
        }

        [Fact]
        public void Attachment_ShouldCreateMessageWithMultipleAttachments()
        {
            var text = Guid.NewGuid().ToString();
            var ssml = Guid.NewGuid().ToString();
            var inputHint = InputHints.ExpectingInput;

            var attachmentName1 = Guid.NewGuid().ToString();
            var attachment1 = new Attachment
            {
                Name = attachmentName1,
            };

            var attachmentName2 = Guid.NewGuid().ToString();
            var attachment2 = new Attachment
            {
                Name = attachmentName2,
            };

            var multipleAttachments = new List<Attachment> { attachment1, attachment2 };

            var message = MessageFactory.Attachment(multipleAttachments, text, ssml, inputHint);

            Assert.Equal(message.Text, text);
            Assert.Equal(ActivityTypes.Message, message.Type);
            Assert.Equal(message.InputHint, inputHint);
            Assert.Equal(message.Speak, ssml);
            Assert.True(message.AttachmentLayout == AttachmentLayoutTypes.List);
            Assert.True(message.Attachments.Count == 2, "Incorrect Attachment Count");
            Assert.True(message.Attachments[0].Name == attachmentName1, "Incorrect Attachment1 Name");
            Assert.True(message.Attachments[1].Name == attachmentName2, "Incorrect Attachment2 Name");
        }

        [Fact]
        public void Attachment_ShouldCreateMessageWithMultipleUnorderedAttachments()
        {
            var text = Guid.NewGuid().ToString();
            var ssml = Guid.NewGuid().ToString();
            var inputHint = InputHints.ExpectingInput;

            var attachmentName1 = Guid.NewGuid().ToString();
            var attachment1 = new Attachment
            {
                Name = attachmentName1,
            };

            var attachmentName2 = Guid.NewGuid().ToString();
            var attachment2 = new Attachment
            {
                Name = attachmentName2,
            };

            var multipleAttachments = new HashSet<Attachment> { attachment1, attachment2 };
            var names = new HashSet<string> { attachmentName1, attachmentName2 };

            var message = MessageFactory.Attachment(multipleAttachments, text, ssml, inputHint);

            Assert.Equal(message.Text, text);
            Assert.Equal(ActivityTypes.Message, message.Type);
            Assert.Equal(message.InputHint, inputHint);
            Assert.Equal(message.Speak, ssml);
            Assert.True(message.AttachmentLayout == AttachmentLayoutTypes.List);
            Assert.True(message.Attachments.Count == 2, "Incorrect Attachment Count");
            Assert.True(names.SetEquals(message.Attachments.Select(a => a.Name)), "Incorrect set of attachment names.");
        }

        [Fact]
        public void ContentUrl_ShouldCreateMessage()
        {
            var text = Guid.NewGuid().ToString();
            var ssml = Guid.NewGuid().ToString();
            var inputHint = InputHints.ExpectingInput;
            var uri = $"https://{Guid.NewGuid().ToString()}";
            var contentType = MediaTypeNames.Image.Jpeg;
            var name = Guid.NewGuid().ToString();

            var message = MessageFactory.ContentUrl(uri, contentType, name, text, ssml, inputHint);

            Assert.Equal(message.Text, text);
            Assert.Equal(ActivityTypes.Message, message.Type);
            Assert.Equal(message.InputHint, inputHint);
            Assert.Equal(message.Speak, ssml);
            Assert.Single(message.Attachments);
            Assert.True(message.Attachments[0].Name == name, "Incorrect Attachment1 Name");
            Assert.True(message.Attachments[0].ContentType == contentType, "Incorrect contentType");
            Assert.True(message.Attachments[0].ContentUrl == uri, "Incorrect Uri");
        }

        [Fact]
        public void CreateTrace_ShouldCreateTraceActivity()
        {
            var activity = MessageFactory.CreateMessageActivity();
            var channelId = "channelId";
            activity.ChannelId = channelId;
            var name = Guid.NewGuid().ToString();
            var value = new object();
            var contentType = MediaTypeNames.Text.Plain;
            var label = "trace";

            var trace = MessageFactory.CreateTrace(activity, name, value, contentType, label);

            Assert.Equal(ActivityTypes.Trace, trace.Type);
            Assert.Equal(name, trace.Name);
            Assert.Equal(value, trace.Value);
            Assert.Equal(contentType, trace.ValueType);
            Assert.Equal(label, trace.Label);
            Assert.Equal(channelId, trace.ChannelId);
        }

        [Theory]
        [InlineData("", null)]
        [InlineData("Select color", "Select color")]
        public async Task SuggestedActions_ShouldValidateImBack(string inputText, string expectedText)
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("ValidateImBack"));

            async Task ReplyWithImBack(ITurnContext ctx, CancellationToken cancellationToken)
            {
                if (((IMessageActivity)ctx.Activity).Text == "test")
                {
                    var activity = MessageFactory.SuggestedActions(
                        new CardAction[]
                        {
                            new CardAction(type: "imBack", text: "red", title: "redTitle"),
                        },
                        inputText,
                        null,
                        null);

                    await ctx.SendActivityAsync((Activity)activity);
                }
            }

            void ValidateImBack(IActivity activity)
            {
                Assert.True(activity.Type == ActivityTypes.Message);

                var messageActivity = (IMessageActivity)activity;

                Assert.True(messageActivity.Text == expectedText);
                Assert.True(messageActivity.SuggestedActions.Actions.Count == 1, "Incorrect Count");
                Assert.True(messageActivity.SuggestedActions.Actions[0].Type == ActionTypes.ImBack, "Incorrect Action Type");
                Assert.True(messageActivity.SuggestedActions.Actions[0].Text == "red", "incorrect text");
                Assert.True(messageActivity.SuggestedActions.Actions[0].Title == "redTitle", "incorrect text");
            }

            await new TestFlow(adapter, ReplyWithImBack)
                .Send("test")
                .AssertReply(ValidateImBack, "ImBack Did not validate")
                .StartTestAsync();
        }
    }
}

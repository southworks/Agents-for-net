using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Dialogs;
using Microsoft.Agents.Builder.TestBot.Shared.Bots;
using Microsoft.Agents.Core.Models;
using Microsoft.BuilderSamples.Tests.Framework;
using Xunit;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.State;

namespace Microsoft.BuilderSamples.Tests.Bots
{
    public class DialogAndWelcomeBotTests
    {
        [Fact]
        public async Task ReturnsWelcomeCardOnConversationUpdate()
        {
            // Arrange
            var mockRootDialog = SimpleMockFactory.CreateMockDialog<Dialog>(null, "mockRootDialog");

            var memoryStorage = new MemoryStorage();
            var sut = new DialogAndWelcomeBot<Dialog>(new ConversationState(memoryStorage), new UserState(memoryStorage), mockRootDialog.Object, null);
            var conversationUpdateActivity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = new List<ChannelAccount>
                {
                    new ChannelAccount { Id = "theUser" },
                },
                Recipient = new ChannelAccount { Id = "theBot" },
            };
            var testAdapter = new TestAdapter(Channels.Test);

            // Act
            // Note: it is kind of obscure that we need to use OnTurnAsync to trigger OnMembersAdded so we get the card
            await testAdapter.ProcessActivityAsync(conversationUpdateActivity, sut.OnTurnAsync, CancellationToken.None);
            var reply = testAdapter.GetNextReply();

            // Assert
            Assert.Single(reply.Attachments);
            Assert.Equal("application/vnd.microsoft.card.adaptive", reply.Attachments.FirstOrDefault()?.ContentType);
        }
    }
}

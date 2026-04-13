using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Xunit;

namespace Microsoft.Agents.Builder.Testing
{
    public class TestFlowTests
    {
        [Fact]
        public async Task ValidateReplyContains()
        {
            var expectedSubstring = "expected substring";
            await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                {
                    await turnContext.SendActivityAsync(
                        $"String with {expectedSubstring} in it",
                        cancellationToken: cancellationToken);
                })
                .Send("hello")
                .AssertReplyContains(expectedSubstring)
                .StartTestAsync();
        }

        [Fact]
        public async Task ValidateReplyContains_ExceptionWithDescription()
        {
            const string exceptionDescription = "Description message";
            const string stringThatNotSubstring = "some string";
            var message = "Just a sample string".Replace(stringThatNotSubstring, string.Empty);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                    {
                        await turnContext.SendActivityAsync(
                            message,
                            cancellationToken: cancellationToken);
                    })
                    .Send("hello")
                    .AssertReplyContains(stringThatNotSubstring, exceptionDescription)
                    .StartTestAsync();
            });
        }

        [Fact]
        public async Task ValidateDelay()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            await new TestFlow(new TestAdapter())
            .Send("hello")
            .Delay(TimeSpan.FromSeconds(1.1))
            .Send("some text")
            .StartTestAsync();
            sw.Stop();

            Assert.True(sw.Elapsed.TotalSeconds > 1, $"Delay broken, elapsed time {sw.Elapsed}?");
        }

        [Fact]
        public async Task ValidateNoReply()
        {
            const string message = "Just a sample string";
            await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                {
                    await turnContext.SendActivityAsync(
                        message,
                        cancellationToken: cancellationToken);
                })
                .Send("hello")
                .AssertReply(message)
                .AssertNoReply()
                .StartTestAsync();
        }

        [Fact]
        public async Task ValidateNoReply_ExceptionWithDescription()
        {
            const string exceptionDescription = "Description message";
            const string message = "Just a sample string";
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                    {
                        await turnContext.SendActivityAsync(
                            message,
                            cancellationToken: cancellationToken);
                        await turnContext.SendActivityAsync(
                            message,
                            cancellationToken: cancellationToken);
                    })
                    .Send("hello")
                    .AssertReply(message)
                    .AssertNoReply(exceptionDescription)
                    .StartTestAsync();
            });
        }

        [Fact]
        public async Task SendConversationUpdate_WithExplicitMembers_AddsThem()
        {
            var addedIds = new List<string>();
            await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                {
                    if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
                    {
                        foreach (var member in turnContext.Activity.MembersAdded)
                            addedIds.Add(member.Id);
                    }
                })
                .SendConversationUpdate(new[] { new ChannelAccount("alice", "Alice"), new ChannelAccount("bob", "Bob") })
                .StartTestAsync();

            Assert.Contains("alice", addedIds);
            Assert.Contains("bob", addedIds);
        }

        [Fact]
        public async Task SendConversationUpdate_NullMembers_UsesDefaultUser()
        {
            string addedId = null;
            var adapter = new TestAdapter();
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
                {
                    if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate
                        && turnContext.Activity.MembersAdded?.Count > 0)
                    {
                        addedId = turnContext.Activity.MembersAdded[0].Id;
                    }
                })
                .SendConversationUpdate((IEnumerable<ChannelAccount>)null)
                .StartTestAsync();

            Assert.Equal(adapter.Conversation.User.Id, addedId);
        }

        [Fact]
        public async Task SendConversationUpdate_EmptyMembers_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await new TestFlow(new TestAdapter())
                    .SendConversationUpdate(Array.Empty<ChannelAccount>())
                    .StartTestAsync();
            });
        }
    }
}

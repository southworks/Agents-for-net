// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using EmptyAgent;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Transcript;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.SampleTest
{
    public class EmptyAgentTest
    {
        [Fact]
        public async Task Test_EmptyAgentEcho()
        {
            var transcript = new MemoryTranscriptStore();
            var adapter = new TestAdapter
            {
                Conversation = new ConversationReference
                {
                    ChannelId = Channels.Test,
                    Conversation = new ConversationAccount { Id = "conversation-1" },
                    User = new ChannelAccount { Id = "user-1", Role = "user" },
                    Agent = new ChannelAccount { Id = "bot-1", Role = "bot" }
                }
            };
            adapter.Use(new TranscriptLoggerMiddleware(transcript));

            var agent = new MyAgent(new AgentApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await agent.OnTurnAsync(turnContext, cancellationToken);
            })
                .Send(new Activity { Type = ActivityTypes.ConversationUpdate, MembersAdded = new[] { new ChannelAccount { Id = "User1" } } })
                .AssertReply("Hello and Welcome!")
                .Send("hello")
                .AssertReply(
                    (new Activity { Type = ActivityTypes.Message, Text = "You said: hello" }).ApplyConversationReference(adapter.Conversation), 
                    equalityComparer: new ExpectedEgress())
                .StartTestAsync();

            var transcriptActivities = await GetTranscript(Channels.Test, "conversation-1", transcript);
            Assert.Equal(4, transcriptActivities.Count);
        }

        private static async Task<IList<IActivity>> GetTranscript(string channelId, string conversationId, ITranscriptStore transcript)
        {
            var activities = new List<IActivity>();

            string continuationToken = null;
            do
            {
                var pagedResult = await transcript.GetTranscriptActivitiesAsync(channelId, conversationId, continuationToken);
                continuationToken = pagedResult.ContinuationToken;
                activities.AddRange(pagedResult.Items);
            } while(continuationToken != null);

            return activities;
        }
    }

    class ExpectedEgress : IEqualityComparer<IActivity>
    {
        public bool Equals(IActivity expected, IActivity outgoing)
        {
            return 
                expected?.Text == outgoing?.Text 
                && expected?.Type == outgoing?.Type
                && expected?.Conversation?.Id == outgoing?.Conversation?.Id
                && expected?.From?.Id == outgoing?.From?.Id
                && expected?.Recipient?.Id == outgoing?.Recipient?.Id
                && !string.IsNullOrEmpty(outgoing.ReplyToId);
        }
        public int GetHashCode(IActivity obj)
        {
            return obj.Text?.GetHashCode() ?? 0;
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Xunit;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class PersistedStateTests
    {
        [Fact]
        public void Constructor_ShouldUseEmptyState()
        {
            var state = new PersistedState();

            Assert.Empty(state.UserState);
            Assert.Empty(state.ConversationState);
        }

        [Fact]
        public void Constructor_ShouldUseKeys()
        {
            var keys = new PersistedStateKeys { UserState = "user", ConversationState = "conversation" };
            var data = new Dictionary<string, object> {
                { "user", new Dictionary<string, object> { {"user1", "test-user" }} },
                { "conversation", new Dictionary<string, object> {  { "conv1", "test-conv"}} }
            };
            var state = new PersistedState(keys, data);

            Assert.Collection(keys, k => Assert.Equal("user", k), k => Assert.Equal("conversation", k));
            Assert.Single(state.UserState);
            Assert.Single(state.ConversationState);
            Assert.Equal("test-user", state.UserState["user1"]);
            Assert.Equal("test-conv", state.ConversationState["conv1"]);
        }
    }
}

﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.State;

namespace AuthenticationBot
{
    public static class StateExtensions
    {
        public static int MessageCount(this ConversationState state)
        {
            return state.GetValue<int>("countKey");
        }

        public static void MessageCount(this ConversationState state, int value)
        {
            state.SetValue("countKey", value);
        }

        public static int IncrementMessageCount(this ConversationState state)
        {
            var count = state.GetValue<int>("countKey");
            state.SetValue("countKey", ++count);
            return count;
        }
    }
}

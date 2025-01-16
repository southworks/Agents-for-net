﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Testing;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.BotBuilder.Tests
{
    public class OnTurnErrorTests
    {
        [Fact]
        public async Task OnTurnError_Test()
        {
            TestAdapter adapter = new TestAdapter(TestAdapter.CreateConversation("OnTurnError_Test"));
            adapter.OnTurnError = async (context, exception) =>
            {
                if (exception is NotImplementedException)
                {
                    await context.SendActivityAsync(context.Activity.CreateReply(exception.Message));
                }
                else
                {
                    await context.SendActivityAsync("Unexpected exception");
                }
            };

            await new TestFlow(adapter, (context, cancellationToken) =>
                {
                    if (context.Activity.Text == "foo")
                    {
                        context.SendActivityAsync(context.Activity.Text);
                    }

                    if (context.Activity.Text == "NotImplementedException")
                    {
                        throw new NotImplementedException("Test");
                    }

                    return Task.CompletedTask;
                })
                .Send("foo")
                .AssertReply("foo", "passthrough")
                .Send("NotImplementedException")
                .AssertReply("Test")
                .StartTestAsync();
        }
    }
}

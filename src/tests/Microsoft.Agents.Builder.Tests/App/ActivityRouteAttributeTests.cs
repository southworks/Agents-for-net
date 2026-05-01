// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class ActivityRouteAttributeTests
    {
        [Fact]
        public async Task MessageRouteAttribute_Any()
        {
            var app = new TestApp(new AgentApplicationOptions((IStorage) null));
            var turnContext = new Mock<ITurnContext>();
            turnContext
                .Setup(c => c.Activity)
                .Returns(new Activity() { Type = ActivityTypes.Message });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnAnyMessageAsync", app.calls[0]);
        }

        [Fact]
        public async Task MessageRouteAttribute_Text()
        {
            var app = new TestApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext
                .Setup(c => c.Activity)
                .Returns(new Activity() { Type = "message", Text = "-test" });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnTestAsync", app.calls[0]);
        }

        [Fact]
        public async Task MessagRouteAttribute_Regex()
        {
            var app = new TestApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext
                .Setup(c => c.Activity)
                .Returns(new Activity() { Type = "message", Text = "testActivity" });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnRegExAsync", app.calls[0]);
        }
    }

    class TestApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [MessageRoute]
        public Task OnAnyMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add("OnAnyMessageAsync");
            return Task.CompletedTask;
        }

        [MessageRoute(text: "-test")]
        public Task OnTestAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add("OnTestAsync");
            return Task.CompletedTask;
        }

        [MessageRoute(textRegex: "test*.")]
        public Task OnRegExAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add("OnRegExAsync");
            return Task.CompletedTask;
        }
    }
}

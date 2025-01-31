// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Dialogs.Debugging;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.State;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Telemetry;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.BotBuilder.Dialogs.Tests
{
    public class DialogTests
    {
        [Fact]
        public async Task ContinueDialogAsync_ShouldReturnComplete()
        {
            var dialogContext = new DialogContext(new DialogSet(), new TurnContext(new TestAdapter(), new Activity()), new DialogState());
            var dialog = new MockDialog("A");

            var result = await dialog.ContinueDialogAsync(dialogContext);

            Assert.Equal(DialogTurnStatus.Complete, result.Status);
        }

        [Fact]
        public async Task ResumeDialogAsync_ShouldReturnComplete()
        {
            var dialogContext = new DialogContext(new DialogSet(), new TurnContext(new TestAdapter(), new Activity()), new DialogState());
            var dialog = new MockDialog("A");

            var result = await dialog.ResumeDialogAsync(dialogContext, DialogReason.BeginCalled, null);

            Assert.Equal(DialogTurnStatus.Complete, result.Status);
        }

        [Fact]
        public async Task ResumeDialogAsync_ShouldThrowOnResultCancellationToken()
        {
            var dialogContext = new DialogContext(new DialogSet(), new TurnContext(new TestAdapter(), new Activity()), new DialogState());
            var dialog = new MockDialog("A");

            await Assert.ThrowsAsync<ArgumentException>(() => dialog.ResumeDialogAsync(dialogContext, DialogReason.BeginCalled, CancellationToken.None));
        }

        [Fact]
        public async Task RegisterSourceLocation_ShouldSetSourceMap()
        {
            var dialogContext = new DialogContext(new DialogSet(), new TurnContext(new TestAdapter(), new Activity()), new DialogState());
            var dialog = new MockDialog("A");

            DebugSupport.SourceMap = new SourceMap();
            await dialog.BeginDialogAsync(dialogContext);
            DebugSupport.SourceMap.TryGetValue(dialog, out var range);

            Assert.Equal("path", range.Path);
            Assert.Equal(1, range.StartPoint.LineIndex);
            Assert.Equal(2, range.EndPoint.LineIndex);
        }

        private class MockDialog(string id) : Dialog(id)
        {
            public override Task<DialogTurnResult> BeginDialogAsync(DialogContext dc, object options = null, CancellationToken cancellationToken = default)
            {
                RegisterSourceLocation("path", 1);
                return Task.FromResult(new DialogTurnResult(DialogTurnStatus.Complete));
            }
        }
    }
}

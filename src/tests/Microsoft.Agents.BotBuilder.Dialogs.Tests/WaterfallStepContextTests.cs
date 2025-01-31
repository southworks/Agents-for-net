// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Xunit;
using Moq;
using Microsoft.Agents.Core.Interfaces;

namespace Microsoft.Agents.BotBuilder.Dialogs.Tests
{
    public class WaterfallStepContextTests
    {
        [Fact]
        public async Task NextAsync_ShouldThrowOnResultCancellationToken()
        {
            var context = new Mock<ITurnContext>();
            var dc = new DialogContext(new DialogSet(), context.Object, new DialogState());
            var dialog = new WaterfallDialog("id");
            var stepContext = new WaterfallStepContext(dialog, dc, null, null, 0, DialogReason.BeginCalled);

            await Assert.ThrowsAsync<ArgumentException>(() => stepContext.NextAsync(CancellationToken.None));
        }

        [Fact]
        public async Task NextAsync_ShouldThrowOnNextCall()
        {
            var context = new Mock<ITurnContext>();
            var dc = new DialogContext(new DialogSet(), context.Object, new DialogState());
            var dialog = new Mock<WaterfallDialog>("id", null);
            var result = new DialogTurnResult(DialogTurnStatus.Complete);
            dialog.Setup(e => e.ResumeDialogAsync(It.IsAny<DialogContext>(), It.IsAny<DialogReason>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result)
                .Verifiable(Times.Once);
            var stepContext = new WaterfallStepContext(dialog.Object, dc, null, null, 0, DialogReason.BeginCalled);

            await stepContext.NextAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => stepContext.NextAsync());
        }
    }
}

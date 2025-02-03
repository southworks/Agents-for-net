// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
#pragma warning disable SA1402 // File may only contain a single type

using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Telemetry;
using Moq;
using Xunit;

namespace Microsoft.Agents.BotBuilder.Dialogs.Tests
{
    public class DialogContextTests
    {
        [Fact]
        public async Task BeginDialogAsync_ShouldThrowOnNullDialogId()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState();
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            await Assert.ThrowsAsync<ArgumentNullException>(() => dialogContext.BeginDialogAsync(null));
        }

        [Fact]
        public async Task BeginDialogAsync_ShouldThrowOnEmptyDialogId()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState();
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            await Assert.ThrowsAsync<ArgumentNullException>(() => dialogContext.BeginDialogAsync(string.Empty));
        }

        [Fact]
        public async Task BeginDialogAsync_ShouldThrowOnNullDialog()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState();
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            await Assert.ThrowsAsync<ArgumentException>(() => dialogContext.BeginDialogAsync("A"));
        }

        [Fact]
        public async Task PromptAsync_ShouldThrowOnNullDialogId()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState();
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            await Assert.ThrowsAsync<ArgumentNullException>(() => dialogContext.PromptAsync(null, new PromptOptions()));
        }

        [Fact]
        public async Task PromptAsync_ShouldThrowOnEmptyDialogId()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState();
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            await Assert.ThrowsAsync<ArgumentNullException>(() => dialogContext.PromptAsync(string.Empty, new PromptOptions()));
        }

        [Fact]
        public async Task PromptAsync_ShouldThrowOnENullOptions()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState();
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            await Assert.ThrowsAsync<ArgumentNullException>(() => dialogContext.PromptAsync("A", null));
        }

        [Fact]
        public async Task ContinueDialogAsync_ShouldThrowOnNullDialog()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState([
                new DialogInstance{ Id = "A" },
                new DialogInstance{ Id = "B" }
            ]);
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            context.SetupGet(e => e.TurnState)
                .Returns(new TurnContextStateCollection())
                .Verifiable(Times.Exactly(2));

            await Assert.ThrowsAsync<InvalidOperationException>(() => dialogContext.ContinueDialogAsync());
        }

        [Fact]
        public async Task EndDialogAsync_ShouldThrowOnResultCancellationToken()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState([
                new DialogInstance{ Id = "A" }
            ]);
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            await Assert.ThrowsAsync<ArgumentException>(() => dialogContext.EndDialogAsync(CancellationToken.None));
        }

        [Fact]
        public async Task EndDialogAsync_ShouldThrowOnNullDialog()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState([
                new DialogInstance{ Id = "A" },
                new DialogInstance{ Id = "B" }
            ]);
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            await Assert.ThrowsAsync<InvalidOperationException>(() => dialogContext.EndDialogAsync());
        }

        [Fact]
        public async Task CancelAllDialogsAsync_ShouldThrowOnEventValueCancellationToken()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState();
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            await Assert.ThrowsAsync<ArgumentException>(() => dialogContext.CancelAllDialogsAsync(false, "event", CancellationToken.None));
        }

        [Fact]
        public async Task ReplaceDialogAsync_ShouldThrowOnOptionsCancellationToken()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState();
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            await Assert.ThrowsAsync<ArgumentException>(() => dialogContext.ReplaceDialogAsync("A", CancellationToken.None));
        }

        [Fact]
        public async Task RepromptDialogAsync_ShouldThrowOnNullDialog()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState([
                new DialogInstance{ Id = "A" }
            ]);
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            await Assert.ThrowsAsync<InvalidOperationException>(() => dialogContext.RepromptDialogAsync());
        }

        [Fact]
        public void FindDialog_ShouldReturnFromParentDialog()
        {
            var parentContext = new Mock<ITurnContext>();
            var parentDialogs = new DialogSet();
            var parentContainer = new ComponentDialog("A");
            var parentDialog = new WaterfallDialog("B");
            parentContainer.AddDialog(parentDialog);
            parentDialogs.Add(parentContainer);
            var parentState = new DialogState([
                new DialogInstance{ Id = "A" },
            ]);
            var parentDialogContext = new DialogContext(parentDialogs, parentContext.Object, parentState);

            var dialogs = new DialogSet();
            var state = new DialogState([
                new DialogInstance{ Id = "C" }
            ]);
            var dialogContext = new DialogContext(dialogs, parentDialogContext, state);

            var dialog = dialogContext.FindDialog("B");

            Assert.Equal(parentDialog, dialog);
        }

        [Fact]
        public void GetLocale_ShouldReturnDefaultLocaleWhenNotFound()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState();
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            context.SetupGet(e => e.Activity)
                .Throws(new CultureNotFoundException())
                .Verifiable(Times.Once);

            var locale = dialogContext.GetLocale();

            Assert.Equal(Thread.CurrentThread.CurrentCulture.Name, locale);
        }

        [Fact]
        public void GetLocale_ShouldReturnDefaultLocaleWhenIsEmpty()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState();
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            context.SetupGet(e => e.Activity)
                .Returns(new Activity { Locale = string.Empty })
                .Verifiable(Times.Once);

            var locale = dialogContext.GetLocale();

            Assert.Equal(Thread.CurrentThread.CurrentCulture.Name, locale);
        }

        [Fact]
        public void GetLocale_ShouldReturnLocaleFromActivity()
        {
            var dialogs = new DialogSet();
            var context = new Mock<ITurnContext>();
            var state = new DialogState();
            var dialogContext = new DialogContext(dialogs, context.Object, state);

            context.SetupGet(e => e.Activity)
                .Returns(new Activity { Locale = "es-ES" })
                .Verifiable(Times.Once);

            var locale = dialogContext.GetLocale();

            Assert.Equal("es-ES", locale);
        }
    }
}

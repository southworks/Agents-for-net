// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
#pragma warning disable SA1402 // File may only contain a single type

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
    public class DialogContainerTests
    {
        [Fact]
        public void DialogContainer_GetVersion()
        {
            var ds = new TestContainer();
            var version1 = ds.GetInternalVersion_Test();
            Assert.NotNull(version1);

            var ds2 = new TestContainer();
            var version2 = ds.GetInternalVersion_Test();
            Assert.NotNull(version2);
            Assert.Equal(version1, version2);

            ds2.Dialogs.Add(new LamdaDialog((dc, ct) => null) { Id = "A" });
            var version3 = ds2.GetInternalVersion_Test();
            Assert.NotNull(version3);
            Assert.NotEqual(version2, version3);

            var version4 = ds2.GetInternalVersion_Test();
            Assert.NotNull(version3);
            Assert.Equal(version3, version4);

            var ds3 = new TestContainer();
            ds3.Dialogs.Add(new LamdaDialog((dc, ct) => null) { Id = "A" });

            var version5 = ds3.GetInternalVersion_Test();
            Assert.NotNull(version5);
            Assert.Equal(version5, version4);

            ds3.Property = "foobar";
            var version6 = ds3.GetInternalVersion_Test();
            Assert.NotNull(version6);
            Assert.NotEqual(version6, version5);

            var ds4 = new TestContainer()
            {
                Property = "foobar"
            };

            ds4.Dialogs.Add(new LamdaDialog((dc, ct) => null) { Id = "A" });
            var version7 = ds4.GetInternalVersion_Test();
            Assert.NotNull(version7);
            Assert.Equal(version7, version6);
        }

        [Fact]
        public void FindDialog_ShouldReturnDialog()
        {
            var dialog = new WaterfallDialog("A");
            var container = new TestContainer();

            container.Dialogs.Add(dialog);
            var actual = container.FindDialog("A", null);

            Assert.Equal(dialog, actual);
        }

        [Fact]
        public void FindDialog_ShouldReturnDialogFromDialogContext()
        {
            var dialog = new WaterfallDialog("A");
            var container = new TestContainer();
            var dialogSet = new DialogSet();
            var dialogContext = new DialogContext(dialogSet, new TurnContext(new TestAdapter(), new Activity()), new DialogState());

            dialogContext.Dialogs.Add(dialog);
            var actual = container.FindDialog("A", dialogContext);

            Assert.Equal(dialog, actual);
        }

        [Fact]
        public async Task OnDialogEventAsync_ShouldTriggerUnhandledVersionChanged()
        {
            var container = new TestContainer();
            var context = new Mock<ITurnContext>();
            var state = new DialogState([
                new DialogInstance { Id = "A" }
            ]);

            context.Setup(e => e.TraceActivityAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse())
                .Verifiable(Times.Once);

            var dialogContext = new DialogContext(new DialogSet(), context.Object, state);
            var actual = await container.OnDialogEventAsync(dialogContext, new DialogEvent { Name = DialogEvents.VersionChanged }, CancellationToken.None);

            Assert.False(actual);
            Mock.Verify(context);
        }

        [Fact]
        public async Task CheckForVersionChangeAsync_ShouldEmitVersionChangedEvent()
        {
            var container = new TestContainer();
            var context = new Mock<ITurnContext>();
            var state = new DialogState([
                new DialogInstance { Id = "A", Version = "1" }
            ]);
            var dialog = new Mock<WaterfallDialog>("A", null);
            var dialogContext = new DialogContext(new DialogSet(), context.Object, state);

            context.SetupGet(e => e.TurnState)
                .Returns(new TurnContextStateCollection())
                .Verifiable(Times.Once);
            dialog.Setup(e => e.OnDialogEventAsync(It.IsAny<DialogContext>(), It.IsAny<DialogEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false)
                .Verifiable(Times.Once);

            dialogContext.Dialogs.Add(dialog.Object);
            await container.CheckForVersionChangeAsync_Test(dialogContext);

            Mock.Verify(dialog);
        }
    }

    public class TestContainer : DialogContainer
    {
        public string Property { get; set; }

        public override Task<DialogTurnResult> BeginDialogAsync(DialogContext dc, object options = null, CancellationToken cancellationToken = default)
        {
            return dc.EndDialogAsync();
        }

        public override DialogContext CreateChildContext(DialogContext dc)
        {
            return dc;
        }

        public Task CheckForVersionChangeAsync_Test(DialogContext dc)
        {
            return CheckForVersionChangeAsync(dc);
        }

        public string GetInternalVersion_Test()
        {
            return GetInternalVersion();
        }

        protected override string GetInternalVersion()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(base.GetInternalVersion());
            sb.Append(Property ?? string.Empty);

            return StringUtils.Hash(sb.ToString());
        }
    }
}

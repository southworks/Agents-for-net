// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Dialogs;
using Microsoft.Agents.Core.Models;
using Moq;

namespace Microsoft.BotBuilderSamples.Tests.Framework
{
    /// <summary>
    /// Contains utility methods for creating simple mock objects based on <see href="https://github.com/moq/moq">moq</see>/>.
    /// </summary>
    public static class SimpleMockFactory
    {
        /// <summary>
        /// Creates a simple mock dialog.
        /// </summary>
        /// <typeparam name="T">A <see cref="Dialog"/> derived type.</typeparam>
        /// <param name="expectedResult">An object containing the results returned by the dialog ind the Dialog in the <see cref="DialogTurnResult"/>.</param>
        /// <param name="constructorParams">Optional constructor parameters for the dialog.</param>
        /// <returns>A <see cref="Mock{T}"/> object for the desired dialog type.</returns>
        public static Mock<T> CreateMockDialog<T>(object expectedResult = null, params object[] constructorParams)
            where T : Dialog
        {
            var mockDialog = new Mock<T>(constructorParams);
            var mockDialogNameTypeName = typeof(T).Name;
            mockDialog
                .Setup(x => x.BeginDialogAsync(It.IsAny<DialogContext>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(async (DialogContext dialogContext, object options, CancellationToken cancellationToken) =>
                {
                    await dialogContext.Context.SendActivityAsync($"{mockDialogNameTypeName} mock invoked", cancellationToken: cancellationToken);

                    return await dialogContext.EndDialogAsync(expectedResult, cancellationToken);
                });

            return mockDialog;
        }
    }
}

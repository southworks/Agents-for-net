// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Xunit;
using Microsoft.Agents.Core.Models;
using System;

namespace Microsoft.Agents.BotBuilder.Dialogs.Tests
{
    public class RecognizerResultExtensionsTests
    {
        [Fact]
        public void GetTopScoringIntent_ShouldThrowOnNullResult()
        {
            Assert.Throws<ArgumentNullException>(() => RecognizerResultExtensions.GetTopScoringIntent(null));
        }

        [Fact]
        public void GetTopScoringIntent_ShouldThrowOnNullIntents()
        {
            var result = new RecognizerResult { Intents = null };

            Assert.Throws<InvalidOperationException>(() => RecognizerResultExtensions.GetTopScoringIntent(result));
        }
    }
}

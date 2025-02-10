// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Dialogs.Debugging;
using Xunit;

namespace Microsoft.Agents.BotBuilder.Dialogs.Tests.Debugging
{
    public class SourceContextTests
    {
        [Fact]
        public void Constructor_ShouldSetPropertiesWithDefaultValues()
        {
            var context = new SourceContext();

            Assert.Empty(context.CallStack);
        }
    }
}

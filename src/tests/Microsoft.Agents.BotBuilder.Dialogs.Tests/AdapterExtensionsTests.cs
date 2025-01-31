// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using Xunit;

namespace Microsoft.Agents.BotBuilder.Dialogs.Tests
{
    public class AdapterExtensionsTests
    {
        [Fact]
        public void UseStorage_ShouldThrowOnNullStorage()
        {
            var adapter = new Mock<IChannelAdapter>(); 

            Assert.Throws<ArgumentNullException>(() => AdapterExtensions.UseStorage(adapter.Object, null));
        }

        [Fact]
        public void UseBotState_ShouldThrowOnNullBotStates()
        {
            var adapter = new Mock<IChannelAdapter>(); 

            Assert.Throws<ArgumentNullException>(() => AdapterExtensions.UseBotState(adapter.Object, null));
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.Protocols.Primitives;
using Xunit;

namespace Microsoft.Agents.Protocols.Connector.Tests
{
    public class RetryParamTests
    {
        [Fact]
        public void Constructor_ShouldStopRetrying()
        {
            var retryParams = RetryParams.StopRetrying;
            Assert.False(retryParams.ShouldRetry);
        }

        [Fact]
        public void Constructor_ShouldRetryByDefault()
        {
            var retryParams = RetryParams.DefaultBackOff(0);
            Assert.True(retryParams.ShouldRetry);
            Assert.Equal(TimeSpan.FromMilliseconds(50), retryParams.RetryAfter);
        }

        [Fact]
        public void Constructor_ShouldEnforceOnMaxRetries()
        {
            var retryParams = RetryParams.DefaultBackOff(10);
            Assert.False(retryParams.ShouldRetry);
        }

        [Fact]
        public void Constructor_ShouldEnforceOnMaxDelay()
        {
            var retryParams = new RetryParams(TimeSpan.FromSeconds(11), true);
            Assert.Equal(TimeSpan.FromSeconds(10), retryParams.RetryAfter);
        }
    }
}

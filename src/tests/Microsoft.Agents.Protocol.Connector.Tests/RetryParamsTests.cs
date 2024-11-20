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
        public void RetryParams_StopRetryingValidation()
        {
            var retryParams = RetryParams.StopRetrying;
            Assert.False(retryParams.ShouldRetry);
        }

        [Fact]
        public void RetryParams_DefaultBackOffShouldRetryOnFirstRetry()
        {
            var retryParams = RetryParams.DefaultBackOff(0);

            // If this is the first time we retry, it should retry by default
            Assert.True(retryParams.ShouldRetry);
            Assert.Equal(TimeSpan.FromMilliseconds(50), retryParams.RetryAfter);
        }

        [Fact]
        public void RetryParams_DefaultBackOffShouldNotRetryAfter5Retries()
        {
            var retryParams = RetryParams.DefaultBackOff(10);
            Assert.False(retryParams.ShouldRetry);
        }

        [Fact]
        public void RetryParams_DelayOutOfBounds()
        {
            var retryParams = new RetryParams(TimeSpan.FromSeconds(11), true);

            // RetryParams should enforce the upper bound on delay time
            Assert.Equal(TimeSpan.FromSeconds(10), retryParams.RetryAfter);
        }
    }
}

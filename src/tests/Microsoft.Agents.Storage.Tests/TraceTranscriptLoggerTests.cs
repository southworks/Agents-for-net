// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage.Transcript;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Activity = Microsoft.Agents.Core.Models.Activity;

namespace Microsoft.Agents.Storage.Tests
{
    public class TraceTranscriptLoggerTests
    {
        private readonly Activity _activity = new Activity
        {
            Id = "test-id",
            Type = ActivityTypes.Message,
            From = new ChannelAccount { Id = "user-id", Name = "user-name", Role = "user-role" },
            Text = "test-text"
        };

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var logger = new TraceTranscriptLogger();

            Assert.NotNull(logger);
        }

        [Fact]
        public async Task LogActivityAsync_ShouldFailOnNullActivity()
        {
            var logger = new TraceTranscriptLogger();

            await Assert.ThrowsAsync<ArgumentNullException>(() =>  logger.LogActivityAsync(null));
        }

        [Fact]
        public async Task LogActivityAsync_ShouldLogJsonWithTraceActivityTrue()
        {
            var listener = new TestTraceListener();
            Trace.Listeners.Add(listener);
            var logger = new TraceTranscriptLogger(true);

            await logger.LogActivityAsync(_activity);

            string traceOutput = listener.GetMessages();
            Assert.Contains(ProtocolJsonSerializer.ToJson(_activity), traceOutput);

            Trace.Listeners.Remove(listener);
        }

        [Fact]
        public async Task LogActivityAsync_ShouldLogActivityWithTraceActivityFalse()
        {
            var listener = new TestTraceListener();
            Trace.Listeners.Add(listener);
            var logger = new TraceTranscriptLogger(false);

            await logger.LogActivityAsync(_activity);

            string traceOutput = listener.GetMessages();

            if (Debugger.IsAttached)
            {
                Assert.Contains(_activity.Text, traceOutput);
            }
            else
            {
                Assert.DoesNotContain(_activity.Text, traceOutput);
            }

            Trace.Listeners.Remove(listener);
        }

        public class TestTraceListener : TraceListener
        {
            private readonly StringBuilder _messages = new StringBuilder();

            public override void Write(string message)
            {
                _messages.Append(message);
            }

            public override void WriteLine(string message)
            {
                _messages.AppendLine(message);
            }

            public string GetMessages()
            {
                return _messages.ToString();
            }

            public void Clear()
            {
                _messages.Clear();
            }
        }
    }
}

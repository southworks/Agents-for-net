// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Authentication
{
    // Event ID registry for ConfigurationConnections log messages:
    //   1 = Per-connection config (LogConnectionConfig)
    //   Future messages: start from 2
    public partial class ConfigurationConnections
    {
#if !NETSTANDARD
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Connection '{Name}': type={Type}")]
        private static partial void LogConnectionConfig(ILogger logger, string name, string type);
#else
        private static void LogConnectionConfig(ILogger logger, string name, string type)
        {
            logger.LogDebug("Connection '{Name}': type={Type}", name, type);
        }
#endif
    }
}

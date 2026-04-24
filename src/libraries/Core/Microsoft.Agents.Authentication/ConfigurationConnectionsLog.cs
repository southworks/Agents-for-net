// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Authentication
{
    // Event ID registry for ConfigurationConnections log messages:
    //   1 = Per-connection config (LogConnectionConfig)
    //   Future messages: start from 2
    public partial class ConfigurationConnections
    {
#if !NETSTANDARD
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Connection '{Name}': type={Type} settings={Settings}")]
        private static partial void LogConnectionConfig(ILogger logger, string name, string type, string settings);
#else
        private static void LogConnectionConfig(ILogger logger, string name, string type, string settings)
        {
            logger.LogDebug("Connection '{Name}': type={Type} settings={Settings}", name, type, settings);
        }
#endif

        private static string FormatSettings(IConfigurationSection settings)
        {
            if (settings == null)
            {
                return "(none)";
            }

            var parts = new List<string>();
            foreach (var child in settings.GetChildren())
            {
                var value = string.Equals(child.Key, "ClientSecret", StringComparison.OrdinalIgnoreCase)
                    ? "[redacted]"
                    : child.Value ?? "[section]";
                parts.Add($"{child.Key}={value}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "(empty)";
        }
    }
}

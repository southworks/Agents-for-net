// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.BotBuilder.Application.Exceptions
{
    /// <summary>
    /// Base exception for the TeamsAI library.
    /// </summary>
    public class TeamsAIException : Exception
    {
        /// <summary>
        /// Create an instance of the <see cref="TeamsAIException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public TeamsAIException(string message) : base(message)
        {
        }

        /// <summary>
        /// Create an instance of the <see cref="TeamsAIException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="innerException">Inner exception.</param>
        public TeamsAIException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

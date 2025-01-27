// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// The type of streaming message being sent.
    /// </summary>
    public enum StreamType
    {
        /// <summary>
        /// An informative update.
        /// </summary>
        Informative,

        /// <summary>
        /// A chunk of partial message text.
        /// </summary>
        Streaming,

        /// <summary>
        /// The final message.
        /// </summary>
        Final
    }
}

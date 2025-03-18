// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Client
{
    internal interface IAgentChannelInfo
    {
        public string Name { get; internal set; }

        public string DisplayName { get; set; }
    }
}
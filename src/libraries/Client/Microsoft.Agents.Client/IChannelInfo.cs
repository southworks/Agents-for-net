// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Client
{
    public interface IChannelInfo
    {
        public string Alias { get; set; }

        public string DisplayName { get; set; }
    }
}
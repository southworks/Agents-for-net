// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Storage
{
    public class WriteResult : IStoreItem
    {
        public string ETag { get; set; }
    }
}

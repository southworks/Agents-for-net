// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.HeaderPropagation;

namespace Microsoft.Agents.Extensions.Teams.Connector
{
    [HeaderPropagation]
    internal class HeaderPropagation : IHeaderPropagationAttribute
    {
        public static void LoadHeaders(HeaderPropagationEntryCollection collection)
        {
            // Propagate headers to the outgoing request by adding them to the HeaderPropagationEntryCollection.
        }
    }
}

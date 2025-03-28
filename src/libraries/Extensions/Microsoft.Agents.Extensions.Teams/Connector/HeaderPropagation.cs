// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector.HeaderPropagation;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Agents.Extensions.Teams.Connector
{
    // This class could implement an interface to guide customers on how to implement the SetHeaders method.
    [HeaderPropagation]
    internal class HeaderPropagation
    {
        public static IHeaderDictionary SetHeaders()
        {
            // this should be replaced with a method.
            return new HeaderDictionary()
            {
                { "User-Agent", "Teams-123" }
            };
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.A365
{
    /// <summary>
    /// The settings for A365Authorization.
    /// </summary>
    public class A365AuthSettings
    {
        public IList<string> Scopes { get; set; }
    }
}

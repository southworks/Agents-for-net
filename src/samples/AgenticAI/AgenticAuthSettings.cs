// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace AgenticAI;

public class AgenticAuthSettings
{
    public required string ConnectionName { get; set; }
    public IList<string>? Scopes { get; set; }
}

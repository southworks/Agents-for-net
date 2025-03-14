
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Hosting.AspNetCore;

/// <summary>
/// Provides configuration for the <see cref="AgentsHttpClientFactory"/>.
/// </summary>
public class HeaderPropagationOptions
{
    /// <summary>
    /// Gets or sets the headers to be captured by the <see cref="AgentsHttpClientFactory"/> on every HttpClient it creates.
    /// </summary>
    public List<string> Headers { get; } = new List<string>();
}
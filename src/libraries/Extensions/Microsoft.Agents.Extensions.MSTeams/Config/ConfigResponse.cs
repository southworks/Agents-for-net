// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.MSTeams.Config;

/// <summary>
/// Response returned from a Teams configuration fetch or submit invoke.
/// </summary>
public class ConfigResponse
{
    /// <summary>
    /// Gets or sets the response type.
    /// </summary>
    public string ResponseType { get; set; }

    /// <summary>
    /// Gets or sets the configuration response payload.
    /// </summary>
    public object Config { get; set; }

    /// <summary>
    /// Gets or sets optional cache metadata.
    /// </summary>
    public object CacheInfo { get; set; }
}

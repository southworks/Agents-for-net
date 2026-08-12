// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.MSTeams.Messages;

/// <summary>
/// Payload submitted by an O365 connector card HttpPOST action.
/// </summary>
public class ConnectorCardActionQuery
{
    /// <summary>
    /// Gets or sets the submitted action body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the connector card action identifier.
    /// </summary>
    public string ActionId { get; set; }
}

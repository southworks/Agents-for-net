// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.MSTeams.Models;

/// <summary>
/// Teams notification settings serialized into channel data.
/// </summary>
public class TeamsNotification
{
    /// <summary>
    /// Gets or sets whether the Teams client should display a notification.
    /// </summary>
    public bool? Alert { get; set; }

    /// <summary>
    /// Gets or sets whether the Teams client should display an in-meeting notification.
    /// </summary>
    public bool? AlertInMeeting { get; set; }

    /// <summary>
    /// Gets or sets the external resource URL associated with the notification.
    /// </summary>
    public string ExternalResourceUrl { get; set; }
}

/// <summary>
/// Attribution information serialized in the Teams <c>onBehalfOf</c> channel-data property.
/// </summary>
public class TeamsOnBehalfOf
{
    /// <summary>
    /// Gets or sets the attribution item identifier.
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// Gets or sets the mention type.
    /// </summary>
    public string MentionType { get; set; }

    /// <summary>
    /// Gets or sets the Microsoft resource identifier.
    /// </summary>
    public string Mri { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; }
}

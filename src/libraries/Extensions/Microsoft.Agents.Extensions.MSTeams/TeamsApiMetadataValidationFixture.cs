// TEMPORARY PR VALIDATION FIXTURE: remove this file after validating the Teams API metadata CI gate.

namespace Microsoft.Agents.Extensions.MSTeams;

/// <summary>
/// Introduces deliberate Teams API metadata drift for pull-request workflow validation.
/// </summary>
public static class TeamsApiMetadataValidationFixture
{
    /// <summary>
    /// Deliberately exposes a type currently recorded as internal-only and reads an unrecorded member.
    /// </summary>
    public static Microsoft.Teams.Apps.MessageActivity ExposeMessageActivity(
        Microsoft.Teams.Apps.MessageActivity activity)
    {
        _ = activity.Attachments;
        return activity;
    }

    /// <summary>
    /// Deliberately retains an upstream type that is absent from the usage manifest.
    /// </summary>
    internal static Microsoft.Teams.Apps.Files.FileUploadInfo RetainFileUploadInfo(
        Microsoft.Teams.Apps.Files.FileUploadInfo value) => value;
}

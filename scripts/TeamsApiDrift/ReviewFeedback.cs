using System.Text.Json;

namespace Microsoft.Agents.TeamsApiDrift;

internal static class ReviewFeedback
{
    internal static string Create(string validationJson)
    {
        using var document = JsonDocument.Parse(validationJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("valid", out var valid) || valid.ValueKind != JsonValueKind.False ||
            !root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array ||
            errors.GetArrayLength() == 0 || errors.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(e.GetString())))
            throw new InvalidDataException("Correction feedback requires an invalid review with nonempty validation errors.");

        // Canonical decisions/quotes stay in the artifacts. Only actionable errors enter the retry prompt.
        return JsonSerializer.Serialize(new { schemaVersion = 1, valid = false, errors }, ToolJson.Options);
    }
}

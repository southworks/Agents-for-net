using System.Text.Json;
using Microsoft.Agents.TeamsApiDrift;
using Xunit;

namespace Microsoft.Agents.TeamsApiDrift.Tests;

public sealed class ReviewFeedbackTests
{
    [Fact]
    public void CorrectionFeedbackPreservesErrorsWithoutCanonicalQuotesOrDecisions()
    {
        const string error = "Invalid citation range at coverage[known-values]: requested 14..165; valid bounds 1..164; last content line 163.";
        var validation = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, valid = false, errors = new[] { error },
            decisions = new[] { new { evidence = new string('x', 900_000) } }
        });
        var feedback = ReviewFeedback.Create(validation);
        using var document = JsonDocument.Parse(feedback);
        Assert.Equal(new[] { "schemaVersion", "valid", "errors" }, document.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(error, document.RootElement.GetProperty("errors")[0].GetString());
        Assert.True(feedback.Length < 1_000);
        Assert.Contains("decisions", validation);
    }

    [Theory]
    [InlineData("{\"valid\":true,\"errors\":[\"A valid advisory finding must not trigger corrections.\"]}")]
    [InlineData("{\"valid\":false,\"errors\":[]}")]
    [InlineData("{\"valid\":false,\"errors\":[null]}")]
    [InlineData("{\"valid\":false,\"errors\":[\"\"]}")]
    [InlineData("{\"errors\":[\"Missing validity flag.\"]}")]
    public void FeedbackRequiresAnInvalidReviewAndActionableErrors(string json) =>
        Assert.Throws<InvalidDataException>(() => ReviewFeedback.Create(json));
}

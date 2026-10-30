using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.TeamsApiDrift;

// Only the ergonomics reader accepts indexed references. Shared review citation contracts stay unchanged.
internal sealed class ErgonomicsCitationConverter : JsonConverter<ReviewCitation>
{
    internal const string ReferencePrefix = "@indexed/";

    private static readonly JsonSerializerOptions ExplicitOptions = new(ToolJson.Options)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public override ReviewCitation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var value = document.RootElement;

        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("reference", out var reference))
        {
            if (value.EnumerateObject().Count() != 1 || reference.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(reference.GetString()))
                {
                    throw new JsonException("An indexed citation must contain only a nonempty reference ID.");
                }
            return new(ReferencePrefix + reference.GetString());
        }

        var citation = JsonSerializer.Deserialize<ReviewCitation>(value.GetRawText(), ExplicitOptions)
            ?? throw new JsonException("Empty citation.");

        if (citation.Path.StartsWith(ReferencePrefix, StringComparison.Ordinal))
        {
            throw new JsonException("Use the reference field for indexed citations, not a reserved path.");
        }

        return citation;
    }

    public override void Write(Utf8JsonWriter writer, ReviewCitation value, JsonSerializerOptions options)
    {
        if (value.Path.StartsWith(ReferencePrefix, StringComparison.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("reference", value.Path[ReferencePrefix.Length..]);
            writer.WriteEndObject();
        }
        else 
        {
            JsonSerializer.Serialize(writer, value, ExplicitOptions);
        }
    }
}

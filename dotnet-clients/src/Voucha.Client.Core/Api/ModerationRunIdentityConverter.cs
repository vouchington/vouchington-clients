using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

internal abstract class ModerationRunIdentityConverter<T> : JsonConverter<T> where T : class
{
  private const string RequestedRun = "ai_draft_requested_run_id";
  private const string SavedRun = "ai_draft_run_id";

  protected abstract IReadOnlySet<string> ProtectedFields { get; }

  public override bool HandleNull => true;

  public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    using var document = JsonDocument.ParseValue(ref reader);
    var value = document.RootElement;
    if (value.ValueKind != JsonValueKind.Object)
    {
      throw new JsonException("A moderation projection must be an object.");
    }
    if (HasCanonicalStaffFields(value))
    {
      ValidateStaffIdentities(value);
    }
    return value.Deserialize<T>(WithoutThisConverter(options))
        ?? throw new JsonException("A moderation projection cannot be null.");
  }

  public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
  {
    if (value is null)
    {
      throw new JsonException("A moderation projection cannot be null.");
    }
    var projection = JsonSerializer.SerializeToElement(value, WithoutThisConverter(options));
    var isStaff = projection.EnumerateObject().Any(property =>
        ProtectedFields.Contains(property.Name) &&
        (property.Name != SavedRun || property.Value.ValueKind != JsonValueKind.Null));
    if (isStaff)
    {
      ValidateStaffIdentities(projection);
    }
    writer.WriteStartObject();
    foreach (var property in projection.EnumerateObject())
    {
      if (!isStaff && property.Name is RequestedRun or SavedRun)
      {
        continue;
      }
      property.WriteTo(writer);
    }
    writer.WriteEndObject();
  }

  private bool HasCanonicalStaffFields(JsonElement value)
  {
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (var property in value.EnumerateObject())
    {
      var canonical = ProtectedFields.FirstOrDefault(field =>
          string.Equals(field, property.Name, StringComparison.OrdinalIgnoreCase));
      if (canonical is null)
      {
        continue;
      }
      if (!string.Equals(canonical, property.Name, StringComparison.Ordinal) || !seen.Add(canonical))
      {
        throw new JsonException("Protected moderation fields require unique canonical names.");
      }
    }
    return seen.Count != 0;
  }

  private static void ValidateStaffIdentities(JsonElement value)
  {
    if (!value.TryGetProperty(RequestedRun, out var requested) || requested.ValueKind != JsonValueKind.String)
    {
      throw new JsonException("The staff projection requires a requested run identity.");
    }
    if (!value.TryGetProperty(SavedRun, out var saved) ||
        saved.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
    {
      throw new JsonException("The staff projection requires a nullable saved run identity.");
    }
  }

  private static JsonSerializerOptions WithoutThisConverter(JsonSerializerOptions options)
  {
    var inner = new JsonSerializerOptions(options);
    for (var index = inner.Converters.Count - 1; index >= 0; index--)
    {
      if (inner.Converters[index] is ModerationRunIdentityConverter<T>)
      {
        inner.Converters.RemoveAt(index);
      }
    }
    return inner;
  }
}

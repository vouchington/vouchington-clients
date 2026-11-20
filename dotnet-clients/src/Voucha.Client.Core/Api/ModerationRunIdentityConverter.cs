using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

internal abstract class ModerationRunIdentityConverter<T> : JsonConverter<T> where T : class
{
  private const string RequestedRun = "ai_draft_requested_run_id";
  private const string SavedRun = "ai_draft_run_id";

  protected abstract IReadOnlySet<string> ProtectedFields { get; }

  /// <summary>Whether a staff projection may carry a null requested run (no admitted run yet).</summary>
  protected virtual bool AllowsNullRequestedRun => false;

  /// <summary>Records that the decoded value came from a staff projection.</summary>
  protected virtual T MarkStaff(T value) => value;

  /// <summary>Whether a value is known to be a staff projection even when every protected field is null.</summary>
  protected virtual bool IsStaff(T value) => false;

  public override bool HandleNull => true;

  public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    using var document = JsonDocument.ParseValue(ref reader);
    var value = document.RootElement;
    if (value.ValueKind != JsonValueKind.Object)
    {
      throw new JsonException("A moderation projection must be an object.");
    }
    var isStaff = HasCanonicalStaffFields(value);
    if (isStaff)
    {
      ValidateStaffIdentities(value);
    }
    var decoded = value.Deserialize<T>(WithoutThisConverter(options))
        ?? throw new JsonException("A moderation projection cannot be null.");
    return isStaff ? MarkStaff(decoded) : decoded;
  }

  public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
  {
    if (value is null)
    {
      throw new JsonException("A moderation projection cannot be null.");
    }
    var projection = JsonSerializer.SerializeToElement(value, WithoutThisConverter(options));
    var isStaff = IsStaff(value) || projection.EnumerateObject().Any(property =>
        ProtectedFields.Contains(property.Name) &&
        property.Value.ValueKind != JsonValueKind.Null);
    if (isStaff)
    {
      ValidateStaffIdentities(projection);
    }
    writer.WriteStartObject();
    foreach (var property in projection.EnumerateObject())
    {
      if (!isStaff && ProtectedFields.Contains(property.Name))
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

  private void ValidateStaffIdentities(JsonElement value)
  {
    if (!value.TryGetProperty(RequestedRun, out var requested) ||
        !(requested.ValueKind == JsonValueKind.String ||
          (AllowsNullRequestedRun && requested.ValueKind == JsonValueKind.Null)))
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

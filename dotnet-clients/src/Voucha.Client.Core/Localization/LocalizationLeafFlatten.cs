using System.Text.Json;

namespace Voucha.Client.Core.Localization;

public static class LocalizationLeafFlatten
{
  public static Dictionary<string, string> Flatten(
      IReadOnlyDictionary<string, JsonElement> messages)
  {
    ArgumentNullException.ThrowIfNull(messages);
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var (id, element) in messages)
    {
      if (element.ValueKind == JsonValueKind.String)
      {
        values[id] = element.GetString() ?? string.Empty;
        continue;
      }
      if (element.ValueKind != JsonValueKind.Object) continue;
      FlattenObject(id, element, values);
    }
    return values;
  }

  private static void FlattenObject(
      string id,
      JsonElement element,
      Dictionary<string, string> values)
  {
    var kind = element.TryGetProperty("kind", out var kindElement)
        ? kindElement.GetString()
        : null;
    if (kind == "plural" && element.TryGetProperty("forms", out var forms))
    {
      foreach (var form in forms.EnumerateObject())
      {
        values[$"{id}.__plural.{form.Name}"] = form.Value.GetString() ?? string.Empty;
      }
      return;
    }
    if (kind == "select-plural" && element.TryGetProperty("cases", out var cases))
    {
      foreach (var selection in cases.EnumerateObject())
      {
        foreach (var form in selection.Value.EnumerateObject())
        {
          values[$"{id}.__select.{selection.Name}.{form.Name}"] =
              form.Value.GetString() ?? string.Empty;
        }
      }
    }
  }
}

using System.Text.Json;

namespace Voucha.Client.Core.Tests.Api;

/// <summary>
/// Walks a JSON document and collects every object-property key path it contains, e.g.
/// "community.slug" or "posts.p1.author_id" (dynamic entity-map keys are ordinary object
/// properties, so they appear in the path literally, identically on both sides of a diff).
/// Used to diff a fixture body against the same body decoded into a DTO and re-encoded, so a
/// DTO that silently drops a field surfaces as a path present on one side and missing on the
/// other. Array items are walked without an index suffix — every item in "results" shares the
/// "results.<field>" prefix — so field-level gaps are still caught without depending on item
/// count or order lining up between the two sides; only named object properties count as
/// "fields" a DTO can drop.
/// </summary>
internal static class JsonKeyPaths
{
  public static HashSet<string> Extract(string json)
  {
    var paths = new HashSet<string>(StringComparer.Ordinal);
    using var document = JsonDocument.Parse(json);
    Walk(document.RootElement, prefix: string.Empty, paths);
    return paths;
  }

  private static void Walk(JsonElement element, string prefix, HashSet<string> paths)
  {
    switch (element.ValueKind)
    {
      case JsonValueKind.Object:
        foreach (var property in element.EnumerateObject())
        {
          var path = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
          paths.Add(path);
          Walk(property.Value, path, paths);
        }

        break;
      case JsonValueKind.Array:
        foreach (var item in element.EnumerateArray())
        {
          Walk(item, prefix, paths);
        }

        break;
      default:
        break;
    }
  }
}

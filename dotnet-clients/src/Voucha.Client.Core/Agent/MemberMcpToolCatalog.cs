using System.Text.Json;

namespace Voucha.Client.Core.Agent;

/// <summary>Collects the server's bounded, opaque-cursor MCP catalog before publication.</summary>
internal static class MemberMcpToolCatalog
{
  internal const int MaximumTools = 128;

  public static async Task<JsonElement> ListAllAsync(
      Func<string?, CancellationToken, Task<JsonElement>> request,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    var seen = new HashSet<string>(StringComparer.Ordinal);
    var tools = new List<JsonElement>();
    Dictionary<string, object?>? result = null;
    string? cursor = null;
    do
    {
      cancellationToken.ThrowIfCancellationRequested();
      var page = await request(cursor, cancellationToken).ConfigureAwait(false);
      if (page.ValueKind != JsonValueKind.Object ||
          !page.TryGetProperty("tools", out var pageTools) || pageTools.ValueKind != JsonValueKind.Array ||
          tools.Count + pageTools.GetArrayLength() > MaximumTools)
        throw new InvalidDataException("Invalid MCP tool catalog.");
      result ??= page.EnumerateObject().ToDictionary(
          property => property.Name, property => (object?)property.Value.Clone(), StringComparer.Ordinal);
      tools.AddRange(pageTools.EnumerateArray().Select(tool => tool.Clone()));
      if (!page.TryGetProperty("nextCursor", out var next) || next.ValueKind == JsonValueKind.Null)
        cursor = null;
      else if (next.ValueKind == JsonValueKind.String)
      {
        cursor = next.GetString();
        if (cursor is null || !seen.Add(cursor) || seen.Count > 100)
          throw new InvalidDataException("Invalid MCP tool catalog cursor.");
      }
      else throw new InvalidDataException("Invalid MCP tool catalog cursor.");
    } while (cursor is not null);
    result!["tools"] = tools;
    result!.Remove("nextCursor");
    return JsonSerializer.SerializeToElement(result);
  }
}

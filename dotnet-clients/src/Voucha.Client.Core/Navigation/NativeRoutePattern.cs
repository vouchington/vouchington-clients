using System.Text.RegularExpressions;

namespace Voucha.Client.Core.Navigation;

public sealed record NativeRoutePattern(string Template)
{
  public NativeRouteMatch? Match(string pathAndQuery)
  {
    ArgumentNullException.ThrowIfNull(pathAndQuery);

    var normalizedTemplate = NormalizePath(Template);
    var normalizedPath = NormalizePath(pathAndQuery);
    var queryItems = QueryItemsFrom(pathAndQuery);
    var templateSegments = Segments(normalizedTemplate);
    var pathSegments = Segments(normalizedPath);
    var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
    var pathIndex = 0;

    foreach (var segment in templateSegments)
    {
      if (segment == "**")
      {
        parameters["splat"] = SafeUnescapePath(string.Join("/", pathSegments.Skip(pathIndex)));
        return CreateMatch(normalizedPath, normalizedTemplate, parameters, queryItems);
      }

      if (pathIndex >= pathSegments.Length)
      {
        return null;
      }

      var matched = MatchSegment(segment, pathSegments[pathIndex]);
      if (matched is null)
      {
        return null;
      }

      foreach (var pair in matched)
      {
        parameters[pair.Key] = pair.Value;
      }

      pathIndex++;
    }

    return pathIndex == pathSegments.Length
        ? CreateMatch(normalizedPath, normalizedTemplate, parameters, queryItems)
        : null;
  }

  public static string NormalizePath(string path)
  {
    ArgumentNullException.ThrowIfNull(path);

    var normalized = path.Trim();
    var queryIndex = normalized.IndexOfAny(['?', '#']);
    if (queryIndex >= 0)
    {
      normalized = normalized[..queryIndex];
    }

    if (normalized.Length == 0)
    {
      return "/";
    }

    if (!normalized.StartsWith('/'))
    {
      normalized = "/" + normalized;
    }

    return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
  }

  private static NativeRouteMatch CreateMatch(
      string path,
      string template,
      IReadOnlyDictionary<string, string> parameters,
      IReadOnlyDictionary<string, string> queryItems) =>
      new(path, template, parameters, queryItems);

  private static string[] Segments(string path) =>
      path.Split('/', StringSplitOptions.RemoveEmptyEntries);

  private static Dictionary<string, string> QueryItemsFrom(string pathAndQuery)
  {
    var queryStart = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
    if (queryStart < 0 || queryStart == pathAndQuery.Length - 1)
    {
      return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    var fragmentStart = pathAndQuery.IndexOf('#', queryStart);
    var query = fragmentStart < 0
        ? pathAndQuery[(queryStart + 1)..]
        : pathAndQuery[(queryStart + 1)..fragmentStart];
    return query.Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .Where(parts => parts.Length == 2)
        .GroupBy(parts => SafeUnescapeQuery(parts[0]), StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => SafeUnescapeQuery(group.First()[1]),
            StringComparer.Ordinal);
  }

  private static string SafeUnescapeQuery(string value) => SafePercentUnescape(value.Replace("+", "%20", StringComparison.Ordinal));

  private static string SafeUnescapePath(string value) => SafePercentUnescape(value);

  private static string SafePercentUnescape(string value)
  {
    try
    {
      return Uri.UnescapeDataString(value);
    }
    catch (UriFormatException)
    {
      return value;
    }
  }

  private static Dictionary<string, string>? MatchSegment(string pattern, string value)
  {
    if (pattern.StartsWith(':') && pattern[1..].All(IsPlaceholderCharacter))
    {
      return value.Length == 0 ? null : new(StringComparer.Ordinal) { [pattern[1..]] = SafeUnescapePath(value) };
    }

    if (!pattern.Contains(':', StringComparison.Ordinal))
    {
      return pattern == value ? new Dictionary<string, string>(StringComparer.Ordinal) : null;
    }

    var names = new List<string>();
    var regex = Regex.Replace(
        Regex.Escape(pattern),
        @":([A-Za-z0-9_]+)",
        match =>
        {
          names.Add(match.Groups[1].Value);
          return "([^/]+)";
        });
    var result = Regex.Match(value, "^" + regex + "$");
    if (!result.Success)
    {
      return null;
    }

    return names
        .Select((name, index) => new { name, value = SafeUnescapePath(result.Groups[index + 1].Value) })
        .ToDictionary(pair => pair.name, pair => pair.value, StringComparer.Ordinal);
  }

  private static bool IsPlaceholderCharacter(char character) =>
      char.IsLetterOrDigit(character) || character == '_';
}

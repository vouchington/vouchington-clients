using System.Text.RegularExpressions;

namespace Voucha.Client.Core.HnDiscussions;

public static partial class HnDiscussionUrlCollector
{
  public const int Limit = 3;

  public static string? Normalize(string? raw)
  {
    if (string.IsNullOrWhiteSpace(raw) || !Uri.TryCreate(raw, UriKind.Absolute, out var uri))
    {
      return null;
    }
    if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
    {
      return null;
    }
    var builder = new UriBuilder(uri) { Fragment = string.Empty };
    if (!string.IsNullOrEmpty(uri.Host)) builder.Host = uri.Host;
    if (builder.Query.Length > 1)
    {
      var kept = builder.Query.TrimStart('?')
          .Split('&', StringSplitOptions.RemoveEmptyEntries)
          .Where(part => !part.Split('=')[0].StartsWith("utm_", StringComparison.OrdinalIgnoreCase));
      var query = string.Join('&', kept);
      builder.Query = query;
    }
    if (builder.Path.Length > 1 && builder.Path.EndsWith('/'))
    {
      builder.Path = builder.Path.TrimEnd('/');
    }
    return builder.Uri.ToString();
  }

  public static IReadOnlyList<string> Collect(IEnumerable<string?> urls)
  {
    ArgumentNullException.ThrowIfNull(urls);
    var seen = new HashSet<string>(StringComparer.Ordinal);
    var collected = new List<string>();
    foreach (var url in urls)
    {
      var normalized = Normalize(url);
      if (normalized is null || !seen.Add(normalized)) continue;
      collected.Add(url!);
      if (collected.Count == Limit) break;
    }
    return collected;
  }

  public static IReadOnlyList<string> ExtractFromMarkdown(string? markdown)
  {
    if (string.IsNullOrEmpty(markdown)) return [];
    var matches = MarkdownUrlPattern().Matches(markdown)
        .Select(match => match.Value.TrimEnd('.', ',', ';', ':', '!', '?'));
    return Collect(matches);
  }

  [GeneratedRegex(@"https?://[^\s)\]>""]+", RegexOptions.CultureInvariant)]
  private static partial Regex MarkdownUrlPattern();
}

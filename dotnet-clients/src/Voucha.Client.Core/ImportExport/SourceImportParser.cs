using System.Text.RegularExpressions;

namespace Voucha.Client.Core.ImportExport;

internal static partial class SourceImportParser
{
  private static readonly HashSet<string> UrlColumnNames =
      new(StringComparer.OrdinalIgnoreCase) { "url", "xmlurl", "rss_feed_url" };

  public static IReadOnlyList<string> CsvUrls(string text)
  {
    var records = CsvImportParser.Parse(text);
    if (records.Count == 0) return [];

    var urlColumn = records[0].FindIndex(value => UrlColumnNames.Contains(value.Trim()));
    if (urlColumn >= 0)
    {
      return records.Skip(1)
          .Select(row => row[urlColumn].Trim())
          .Where(value => value.Length > 0)
          .ToArray();
    }

    return TsvOrUrlList(text);
  }

  public static IReadOnlyList<string> OpmlUrls(string text)
  {
    var urls = new List<string>();
    foreach (Match outline in OutlinePattern().Matches(text))
    {
      string? xmlUrl = null;
      foreach (Match attribute in AttributePattern().Matches(outline.Value))
      {
        if (attribute.Groups[1].Value.Equals("xmlurl", StringComparison.OrdinalIgnoreCase))
        {
          xmlUrl = attribute.Groups[2].Value;
        }
      }
      if (!string.IsNullOrEmpty(xmlUrl)) urls.Add(xmlUrl);
    }
    return urls;
  }

  private static string[] TsvOrUrlList(string text)
  {
    var lines = text.Split('\n')
        .Select(line => line.Trim())
        .Where(line => line.Length > 0)
        .ToArray();
    if (lines.Length == 0) return [];
    if (!lines[0].Contains('\t', StringComparison.Ordinal)) return lines;

    var headers = lines[0].ToUpperInvariant().Split('\t');
    var urlColumn = Array.IndexOf(headers, "XMLURL");
    if (urlColumn < 0) urlColumn = Array.IndexOf(headers, "URL");
    if (urlColumn < 0) return [];

    return lines.Skip(1)
        .Select(line => line.Split('\t').ElementAtOrDefault(urlColumn) ?? string.Empty)
        .Where(value => value.Length > 0)
        .ToArray();
  }

  [GeneratedRegex("<outline\\s[^>]*?xmlUrl\\s*=\\s*\"([^\"]*)\"[^>]*?/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
  private static partial Regex OutlinePattern();

  [GeneratedRegex("(\\w+)\\s*=\\s*\"([^\"]*)\"", RegexOptions.CultureInvariant)]
  private static partial Regex AttributePattern();
}

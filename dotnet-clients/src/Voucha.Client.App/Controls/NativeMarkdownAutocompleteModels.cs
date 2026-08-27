namespace Voucha.Client.App.Controls;

internal readonly record struct AutocompleteToken(char Marker, string Query, int Start, int End)
{
  public static bool TryParse(string markdown, out AutocompleteToken token)
  {
    token = default;
    if (string.IsNullOrWhiteSpace(markdown)) return false;

    var start = markdown.Length - 1;
    while (start >= 0 && !char.IsWhiteSpace(markdown[start])) start--;
    start++;
    if (start >= markdown.Length) return false;

    var marker = markdown[start];
    if (marker is not ('@' or '#' or '!')) return false;

    var query = markdown[(start + 1)..];
    if (string.IsNullOrWhiteSpace(query)) return false;

    token = new AutocompleteToken(marker, query, start, markdown.Length);
    return true;
  }
}

internal sealed record AutocompleteSuggestion(string Replacement, string Label, string? Detail);

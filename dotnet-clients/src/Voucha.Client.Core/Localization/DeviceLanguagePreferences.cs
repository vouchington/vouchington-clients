namespace Voucha.Client.Core.Localization;

public static class DeviceLanguagePreferences
{
  public static IReadOnlyList<string> Ordered(
      IEnumerable<string>? preferredLanguages,
      string? fallbackLanguage)
  {
    var ordered = new List<string>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (preferredLanguages is not null)
    {
      foreach (var language in preferredLanguages)
      {
        Add(language);
      }
    }
    Add(fallbackLanguage);
    return ordered;

    void Add(string? language)
    {
      if (!string.IsNullOrWhiteSpace(language) && seen.Add(language))
      {
        ordered.Add(language);
      }
    }
  }
}

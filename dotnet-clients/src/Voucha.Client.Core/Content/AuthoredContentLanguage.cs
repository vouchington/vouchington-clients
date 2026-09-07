namespace Voucha.Client.Core.Content;

public enum AuthoredTextDirection { LeftToRight, RightToLeft }

public sealed record AuthoredContentLanguage(string? Tag, AuthoredTextDirection? Direction)
{
  private static readonly HashSet<string> RightToLeft = new(StringComparer.Ordinal)
  {
    "ar", "fa", "he", "ur",
  };
  private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
  {
    "af", "sq", "ar", "hy", "az", "eu", "be", "bn", "nb", "bs", "bg", "ca", "zh", "hr", "cs", "da", "nl", "en", "eo", "et", "fi", "fr", "lg", "ka", "de", "el", "gu", "he", "hi", "hu", "is", "id", "ga", "it", "ja", "kk", "ko", "la", "lv", "lt", "mk", "ms", "mi", "mr", "mn", "nn", "fa", "pl", "pt", "pa", "ro", "ru", "sr", "sn", "sk", "sl", "so", "st", "es", "sw", "sv", "tl", "ta", "te", "th", "ts", "tn", "tr", "uk", "ur", "vi", "cy", "xh", "yo", "zu",
  };

  public static AuthoredContentLanguage Resolve(string? declaredLanguage, string? detectedLanguage)
  {
    foreach (var candidate in new[] { declaredLanguage, detectedLanguage })
    {
      var tag = Normalize(candidate);
      if (tag is null) continue;
      return new(tag, RightToLeft.Contains(tag)
          ? AuthoredTextDirection.RightToLeft
          : AuthoredTextDirection.LeftToRight);
    }
    return new(null, null);
  }

  private static string? Normalize(string? candidate)
  {
    var language = candidate?.Trim().Split(['-', '_'], 2)[0];
    return string.IsNullOrEmpty(language)
        ? null
        : Supported.FirstOrDefault(code => string.Equals(code, language, StringComparison.OrdinalIgnoreCase));
  }
}

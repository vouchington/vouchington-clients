namespace Voucha.Client.Core;

internal static class TurnstileSiteKeys
{
  internal const string AlwaysPassSiteKey = "1x00000000000000000000AA";
  internal const string MissingReleaseKeyMessage =
      "Release builds require an explicit non-test VOUCHA_TURNSTILE_SITE_KEY.";
  internal const string RejectedTestKeyMessage =
      "Release builds reject Cloudflare Turnstile test site keys.";

  internal static readonly IReadOnlySet<string> TestSiteKeys = new HashSet<string>(StringComparer.Ordinal)
  {
    AlwaysPassSiteKey,
    "2x00000000000000000000AB",
    "1x00000000000000000000BB",
    "2x00000000000000000000BB",
    "3x00000000000000000000FF",
    "1x0000000000000000000000000000000AA",
    "2x0000000000000000000000000000000AA",
    "3x0000000000000000000000000000000AA",
  };

  internal static string Require(string? supplied) =>
#if DEBUG
      Resolve(supplied, release: false);
#else
      Resolve(supplied, release: true);
#endif

  internal static string Resolve(string? supplied, bool release)
  {
    var key = supplied?.Trim() ?? "";
    if (!release)
    {
      return key.Length == 0 ? AlwaysPassSiteKey : key;
    }

    if (key.Length == 0)
    {
      throw new InvalidOperationException(MissingReleaseKeyMessage);
    }

    if (TestSiteKeys.Contains(key))
    {
      throw new InvalidOperationException(RejectedTestKeyMessage);
    }

    return key;
  }
}

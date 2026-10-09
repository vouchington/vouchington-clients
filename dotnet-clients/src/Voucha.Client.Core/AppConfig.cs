using System.Globalization;

namespace Voucha.Client.Core;

public sealed record AppConfig(
    Uri ApiBaseUrl,
    string TurnstileSiteKey = "",
    bool AllowPostComposeCaptchaBypass = false,
    bool AllowCommunityCreateCaptchaBypass = false,
    Uri? ImageBaseUrl = null,
    Uri? WebBaseUrl = null,
    string? AppleClientId = null)
{
  public const string DefaultApiBaseUrl = "https://voucha.ai";
  public const string DefaultImageBaseUrl = "https://images.voucha.ai";
  public const string DefaultWebBaseUrl = "https://voucha.ai";

  public string RequiredTurnstileSiteKey =>
      !string.IsNullOrWhiteSpace(TurnstileSiteKey)
          ? TurnstileSiteKey
          : throw new InvalidOperationException("Missing VOUCHA_TURNSTILE_SITE_KEY");

  public static AppConfig FromEnvironment(
      IReadOnlyDictionary<string, string?>? environment = null)
  {
    environment ??= Environment.GetEnvironmentVariables()
        .Cast<System.Collections.DictionaryEntry>()
        .ToDictionary(
            entry => Convert.ToString(entry.Key, CultureInfo.InvariantCulture)!,
            entry => Convert.ToString(entry.Value, CultureInfo.InvariantCulture),
            StringComparer.Ordinal);

    var rawBaseUrl = Read(environment, "VOUCHA_API_BASE_URL") ?? DefaultApiBaseUrl;
    if (!Uri.TryCreate(rawBaseUrl, UriKind.Absolute, out var baseUrl))
    {
      throw new InvalidOperationException($"Invalid VOUCHA_API_BASE_URL: {rawBaseUrl}");
    }

    var turnstileSiteKey = Read(environment, "VOUCHA_TURNSTILE_SITE_KEY") ??
        Read(environment, "NEXT_PUBLIC_CLOUDFLARE_TURNSTILE_SITE_KEY");

    return new AppConfig(
        baseUrl,
        turnstileSiteKey ?? "",
        ReadBool(environment, "VOUCHA_POST_COMPOSE_CAPTCHA_BYPASS"),
        ReadBool(environment, "VOUCHA_COMMUNITY_CREATE_CAPTCHA_BYPASS"),
        ReadUri(environment, "VOUCHA_IMAGE_BASE_URL", DefaultImageBaseUrl),
        ReadUri(environment, "VOUCHA_WEB_BASE_URL", Read(environment, "SITEMAP_BASE_URL") ?? DefaultWebBaseUrl),
        Read(environment, "VOUCHA_APPLE_CLIENT_ID") ??
            Read(environment, "NEXT_PUBLIC_APPLE_CLIENT_ID") ??
            Read(environment, "APPLE_CLIENT_ID"));
  }

  public Uri? ImageUrlForPlacement(string? placementId, int revision, string? imageId, int width = 96)
  {
    if (string.IsNullOrWhiteSpace(placementId) || string.IsNullOrWhiteSpace(imageId))
    {
      return null;
    }

    var builder = new UriBuilder(ImageBaseUrl ?? new Uri(DefaultImageBaseUrl));
    builder.Path = $"{builder.Path.TrimEnd('/')}/images/placements/{Uri.EscapeDataString(placementId)}/{revision}/{Uri.EscapeDataString(imageId)}";
    builder.Query = $"w={width}";
    return builder.Uri;
  }

  private static string? Read(IReadOnlyDictionary<string, string?> environment, string key) =>
      environment.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
          ? value
          : null;

  private static Uri ReadUri(
      IReadOnlyDictionary<string, string?> environment,
      string key,
      string defaultValue)
  {
    var rawValue = Read(environment, key) ?? defaultValue;
    if (!Uri.TryCreate(rawValue, UriKind.Absolute, out var value))
    {
      throw new InvalidOperationException($"Invalid {key}: {rawValue}");
    }

    return value;
  }

  private static bool ReadBool(IReadOnlyDictionary<string, string?> environment, string key)
  {
    var value = Read(environment, key);
    return value is not null && bool.TryParse(value, out var parsed) && parsed;
  }
}

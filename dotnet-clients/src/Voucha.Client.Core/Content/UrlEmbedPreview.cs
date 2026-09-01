using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Content;

public sealed record UrlEmbedPreview(
    string? Title,
    string? Description,
    string? Provider,
    Uri? ThumbnailUrl,
    Uri? SourceUrl,
    Uri? PlayerUrl)
{
  public bool CanPlay => PlayerUrl is not null && UrlEmbedPreviews.IsValidSource(SourceUrl);
  public bool HasPreview => Title is not null || Description is not null || Provider is not null ||
      ThumbnailUrl is not null || SourceUrl is not null || PlayerUrl is not null;
  public bool HasSource => SourceUrl is not null;
}

public static class UrlEmbedPreviews
{
  public static UrlEmbedPreview From(UrlEmbed? embed)
  {
    var tags = embed?.MetaTags;
    var source = SafeUri(embed?.SourceUrl);
    return new(
        First(embed?.Title, Tag(tags, "og:title"), Tag(tags, "twitter:title")),
        First(embed?.Description, Tag(tags, "og:description"), Tag(tags, "twitter:description")),
        First(embed?.ProviderName, Tag(tags, "og:site_name"), source?.Host),
        SafeUri(embed?.ThumbnailUrl),
        source,
        SafePlayer(embed?.PlayerUrl));
  }

  public static bool IsApprovedPlayer(Uri? candidate) =>
      candidate is { IsAbsoluteUri: true, Scheme: "https", UserInfo: "", Host: not "" } &&
      HasExactHostAuthority(candidate) &&
      ((candidate.Host == "www.youtube-nocookie.com" && candidate.AbsolutePath.StartsWith("/embed/", StringComparison.Ordinal)) ||
       (candidate.Host == "player.vimeo.com" && candidate.AbsolutePath.StartsWith("/video/", StringComparison.Ordinal)));

  public static bool IsValidSource(Uri? candidate) =>
      candidate is { IsAbsoluteUri: true, Scheme: "https", UserInfo: "" } &&
      !string.IsNullOrEmpty(candidate.Host) &&
      HasExactHostAuthority(candidate);

  private static bool HasExactHostAuthority(Uri candidate)
  {
    var authority = candidate.OriginalString["https://".Length..].Split(['/', '?', '#'], 2)[0];
    return string.Equals(authority, candidate.Host, StringComparison.Ordinal);
  }

  private static Uri? SafePlayer(string? value)
  {
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !IsApprovedPlayer(uri)) return null;
    return uri;
  }

  private static Uri? SafeUri(string? value) =>
      Uri.TryCreate(value, UriKind.Absolute, out var uri) && IsValidSource(uri) ? uri : null;

  private static string? Tag(IReadOnlyDictionary<string, JsonElement>? tags, string key)
  {
    if (tags is null) return null;
    foreach (var (candidate, value) in tags)
    {
      if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase) && value.ValueKind == JsonValueKind.String)
        return value.GetString();
    }
    return null;
  }

  private static string? First(params string?[] values)
  {
    foreach (var value in values)
    {
      var trimmed = value?.Trim();
      if (!string.IsNullOrEmpty(trimmed)) return trimmed;
    }

    return null;
  }
}

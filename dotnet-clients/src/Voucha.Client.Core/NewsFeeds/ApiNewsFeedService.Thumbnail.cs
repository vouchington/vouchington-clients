namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class ApiNewsFeedService
{
  private static Uri? SelectThumbnailUrl(Uri? thumbnailUrl, string? thumbnailSidecarUrl)
  {
    if (TryParseThumbnailUrl(thumbnailSidecarUrl, out var parsedThumbnailUrl))
    {
      return parsedThumbnailUrl;
    }

    return thumbnailUrl;
  }

  private static bool TryParseThumbnailUrl(string? thumbnailUrl, out Uri? parsedThumbnailUrl)
  {
    parsedThumbnailUrl = null;
    return thumbnailUrl is not null &&
        Uri.TryCreate(thumbnailUrl, UriKind.RelativeOrAbsolute, out parsedThumbnailUrl);
  }
}

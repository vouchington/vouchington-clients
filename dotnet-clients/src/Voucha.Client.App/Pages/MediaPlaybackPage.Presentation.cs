using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;

namespace Voucha.Client.App.Pages;

public partial class MediaPlaybackPage
{
  private static UiText MediaMetadataText(NewsFeedItem item)
  {
    if (!item.IsMedia || string.IsNullOrWhiteSpace(item.ProtocolMediaType))
    {
      return UiText.Verbatim(string.Empty);
    }

    return string.IsNullOrWhiteSpace(item.VideoPlatform)
        ? UiText.Localized(
            UiMessageKey.NativeDotnetMediaPlaybackMediaType,
            ("mediaType", UiTaxonomy.MediaType(item.ProtocolMediaType)))
        : UiText.Localized(
            UiMessageKey.NativeDotnetMediaPlaybackMediaTypeWithPlatform,
            ("mediaType", UiTaxonomy.MediaType(item.ProtocolMediaType)),
            ("videoPlatform", UiText.Verbatim(item.VideoPlatform)));
  }
}

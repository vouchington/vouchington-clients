using Voucha.Client.Core.NewsFeeds;

namespace Voucha.Client.App.Pages;

public partial class NewsFeedsPage
{
  private async void OnEmbedPlayClicked(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: NewsFeedItem { EmbedPreview: { PlayerUrl: { } playerUrl, SourceUrl: { } sourceUrl } } })
      await Navigation.PushAsync(new EmbedPlayerPage(playerUrl, sourceUrl)).ConfigureAwait(true);
  }

  private async void OnEmbedOpenClicked(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: NewsFeedItem { EmbedPreview.SourceUrl: { } sourceUrl } })
      await Launcher.Default.OpenAsync(sourceUrl).ConfigureAwait(true);
  }
}

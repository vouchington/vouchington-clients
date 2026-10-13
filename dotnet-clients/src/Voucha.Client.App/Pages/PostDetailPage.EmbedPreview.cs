namespace Voucha.Client.App.Pages;

public partial class PostDetailPage
{
  private async void OnEmbedPlayClicked(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: PostDetailPageRow { VisibleEmbedPreview: { PlayerUrl: { } playerUrl, SourceUrl: { } sourceUrl } } })
      await Navigation.PushAsync(new EmbedPlayerPage(playerUrl, sourceUrl)).ConfigureAwait(true);
  }

  private async void OnEmbedOpenClicked(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: PostDetailPageRow { VisibleEmbedPreview.SourceUrl: { } sourceUrl } })
      await Launcher.Default.OpenAsync(sourceUrl).ConfigureAwait(true);
  }
}

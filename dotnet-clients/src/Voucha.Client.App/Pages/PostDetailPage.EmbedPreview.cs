namespace Voucha.Client.App.Pages;

public partial class PostDetailPage
{
  private async void OnEmbedPlayClicked(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: PostDetailPageRow { EmbedPreview: { PlayerUrl: { } playerUrl, SourceUrl: { } sourceUrl } } })
      await Navigation.PushAsync(new EmbedPlayerPage(playerUrl, sourceUrl)).ConfigureAwait(true);
  }

  private async void OnEmbedOpenClicked(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: PostDetailPageRow { EmbedPreview.SourceUrl: { } sourceUrl } })
      await Launcher.Default.OpenAsync(sourceUrl).ConfigureAwait(true);
  }
}

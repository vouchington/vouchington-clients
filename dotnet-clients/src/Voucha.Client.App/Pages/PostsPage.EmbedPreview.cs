using Voucha.Client.Core.Posts;

namespace Voucha.Client.App.Pages;

public partial class PostsPage
{
  private async void OnEmbedPlayClicked(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: PostRow { EmbedPreview: { PlayerUrl: { } playerUrl, SourceUrl: { } sourceUrl } } })
      await Navigation.PushAsync(new EmbedPlayerPage(playerUrl, sourceUrl)).ConfigureAwait(true);
  }

  private async void OnEmbedOpenClicked(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: PostRow { EmbedPreview.SourceUrl: { } sourceUrl } })
      await Launcher.Default.OpenAsync(sourceUrl).ConfigureAwait(true);
  }
}

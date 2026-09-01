using Microsoft.Maui.ApplicationModel;
using Voucha.Client.Core.Posts;

namespace Voucha.Client.App.Pages;

public partial class ProfilePage
{
  private async void OnHistoryEmbedPlayClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: PostRow { EmbedPreview: { PlayerUrl: { } playerUrl, SourceUrl: { } sourceUrl } } })
      await Navigation.PushAsync(new EmbedPlayerPage(playerUrl, sourceUrl)).ConfigureAwait(true);
  }

  private async void OnHistoryEmbedOpenClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: PostRow { EmbedPreview.SourceUrl: { } sourceUrl } })
      await Launcher.Default.OpenAsync(sourceUrl).ConfigureAwait(true);
  }
}

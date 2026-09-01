using Voucha.Client.Core.Communities;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private async void OnEmbedPlayClicked(object? sender, EventArgs args)
  {
    if (sender is Button { BindingContext: CommunitySummaryRow { EmbedPreview: { PlayerUrl: { } playerUrl, SourceUrl: { } sourceUrl } } })
      await Navigation.PushAsync(new EmbedPlayerPage(playerUrl, sourceUrl)).ConfigureAwait(true);
  }

  private async void OnEmbedOpenClicked(object? sender, EventArgs args)
  {
    if (sender is Button { BindingContext: CommunitySummaryRow { EmbedPreview.SourceUrl: { } sourceUrl } })
      await Launcher.Default.OpenAsync(sourceUrl).ConfigureAwait(true);
  }
}

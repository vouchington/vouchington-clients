namespace Voucha.Client.App.Pages;

public sealed partial class RssFeedItemDetailPage
{
  private async void OnEmbedPlayClicked(object? sender, EventArgs args)
  {
    if (viewModel.Detail?.Item.EmbedPreview is { PlayerUrl: { } playerUrl, SourceUrl: { } sourceUrl })
      await Navigation.PushAsync(new EmbedPlayerPage(playerUrl, sourceUrl)).ConfigureAwait(true);
  }

  private async void OnEmbedOpenClicked(object? sender, EventArgs args)
  {
    if (viewModel.Detail?.Item.EmbedPreview?.SourceUrl is { } sourceUrl)
      await Launcher.Default.OpenAsync(sourceUrl).ConfigureAwait(true);
  }

  private static VerticalStackLayout CreateEmbedPreview()
  {
    var image = new Image { HeightRequest = 180, Aspect = Aspect.AspectFit };
    image.SetBinding(Image.SourceProperty, "Detail.Item.EmbedPreview.ThumbnailUrl");
    var provider = new Label { FontSize = 13, FontAttributes = FontAttributes.Bold };
    provider.SetBinding(Label.TextProperty, "Detail.Item.EmbedPreview.Provider");
    var title = new Label { FontSize = 18, FontAttributes = FontAttributes.Bold };
    title.SetBinding(Label.TextProperty, "Detail.Item.EmbedPreview.Title");
    var description = new Label { FontSize = 14 };
    description.SetBinding(Label.TextProperty, "Detail.Item.EmbedPreview.Description");
    var preview = new VerticalStackLayout { Spacing = 4, Children = { image, provider, title, description } };
    preview.SetBinding(IsVisibleProperty, "Detail.Item.EmbedPreview.HasPreview");
    return preview;
  }
}

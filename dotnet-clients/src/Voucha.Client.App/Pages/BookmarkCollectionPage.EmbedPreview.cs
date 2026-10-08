using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed partial class BookmarkCollectionPage
{
  private VerticalStackLayout CreateEmbedPreview()
  {
    var image = new Image { HeightRequest = 160, Aspect = Aspect.AspectFit };
    image.SetBinding(Image.SourceProperty, "EmbedPreview.ThumbnailUrl");
    var provider = new Label { FontSize = 12, FontAttributes = FontAttributes.Bold };
    provider.SetBinding(Label.TextProperty, "EmbedPreview.Provider");
    var title = new Label { FontAttributes = FontAttributes.Bold };
    title.SetBinding(Label.TextProperty, "EmbedPreview.Title");
    var description = new Label { FontSize = 12 };
    description.SetBinding(Label.TextProperty, "EmbedPreview.Description");
    var play = new Button { Text = UiCopy.Localize(UiMessageKey.NativeDotnetMediaPlaybackPlay) };
    play.SetBinding(IsVisibleProperty, "EmbedPreview.CanPlay");
    play.Clicked += OnEmbedPlayClicked;
    var open = new Button { Text = UiCopy.Localize(UiMessageKey.NativeSwiftPodcastPlaybackOpenSource) };
    open.SetBinding(IsVisibleProperty, "EmbedPreview.HasSource");
    open.Clicked += OnEmbedOpenClicked;
    var preview = new VerticalStackLayout { Spacing = 4, Children = { image, provider, title, description, play, open } };
    preview.SetBinding(IsVisibleProperty, "EmbedPreview.HasPreview");
    return preview;
  }

  private async void OnEmbedPlayClicked(object? sender, EventArgs args)
  {
    if (sender is Button { BindingContext: BookmarkCollectionRow { EmbedPreview: { PlayerUrl: { } playerUrl, SourceUrl: { } sourceUrl } } })
      await Navigation.PushAsync(new EmbedPlayerPage(playerUrl, sourceUrl)).ConfigureAwait(true);
  }

  private async void OnEmbedOpenClicked(object? sender, EventArgs args)
  {
    if (sender is Button { BindingContext: BookmarkCollectionRow { EmbedPreview.SourceUrl: { } sourceUrl } })
      await Launcher.Default.OpenAsync(sourceUrl).ConfigureAwait(true);
  }
}

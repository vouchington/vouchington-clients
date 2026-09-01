using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

/// <summary>Narrow, user-initiated player for backend-approved YouTube and Vimeo URLs.</summary>
public sealed class EmbedPlayerPage : ContentPage
{
  private readonly Uri playerUrl;
  private readonly ProviderEmbedWebView player = new();

  public EmbedPlayerPage(Uri playerUrl, Uri sourceUrl)
  {
    if (!UrlEmbedPreviews.IsApprovedPlayer(playerUrl)) throw new ArgumentException("Unsupported player URL.", nameof(playerUrl));
    if (!UrlEmbedPreviews.IsValidSource(sourceUrl)) throw new ArgumentException("Unsupported source URL.", nameof(sourceUrl));
    this.playerUrl = playerUrl;
    Title = UiCopy.Localize(UiMessageKey.NativeDotnetMediaPlaybackPlay);
    var open = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeSwiftPodcastPlaybackOpenSource);
    open.Clicked += async (_, _) => await Launcher.Default.OpenAsync(sourceUrl).ConfigureAwait(true);
    Content = new VerticalStackLayout { Padding = 20, Spacing = 8, Children = { player, open } };
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();
    player.Load(playerUrl);
  }

  protected override void OnDisappearing()
  {
    player.Unload();
    base.OnDisappearing();
  }

}

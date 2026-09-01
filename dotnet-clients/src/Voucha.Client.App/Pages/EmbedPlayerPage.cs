using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

/// <summary>Narrow, user-initiated player for backend-approved YouTube and Vimeo URLs.</summary>
public sealed class EmbedPlayerPage : ContentPage
{
  private readonly Uri playerUrl;
  private readonly VerticalStackLayout playerLayout;
  private ProviderEmbedWebView? player;

  public EmbedPlayerPage(Uri playerUrl, Uri sourceUrl)
  {
    if (!UrlEmbedPreviews.IsApprovedPlayer(playerUrl)) throw new ArgumentException("Unsupported player URL.", nameof(playerUrl));
    if (!UrlEmbedPreviews.IsValidSource(sourceUrl)) throw new ArgumentException("Unsupported source URL.", nameof(sourceUrl));
    this.playerUrl = playerUrl;
    Title = UiCopy.Localize(UiMessageKey.NativeDotnetMediaPlaybackPlay);
    var open = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeSwiftPodcastPlaybackOpenSource);
    open.Clicked += async (_, _) => await Launcher.Default.OpenAsync(sourceUrl).ConfigureAwait(true);
    playerLayout = new VerticalStackLayout { Padding = 20, Spacing = 8, Children = { open } };
    Content = playerLayout;
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();
    if (player is null)
    {
      player = new ProviderEmbedWebView();
      playerLayout.Insert(0, player);
    }
    player.Load(playerUrl);
  }

  protected override void OnDisappearing()
  {
    if (player is { } active)
    {
      player = null;
      active.Unload();
      playerLayout.Remove(active);
    }
    base.OnDisappearing();
  }
}

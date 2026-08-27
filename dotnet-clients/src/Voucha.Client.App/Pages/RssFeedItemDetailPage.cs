using Microsoft.Maui.ApplicationModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.HnDiscussions;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Localization;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.FollowerDistributions;

namespace Voucha.Client.App.Pages;

public sealed class RssFeedItemDetailPage : ContentPage
{
  private readonly RssFeedItemDetailViewModel viewModel;
  private readonly IServiceProvider serviceProvider;
  private readonly HnDiscussionsViewModel hnDiscussions;
  private readonly ISessionStore sessionStore;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private readonly Button followerSend;
  private bool isSessionChangedAttached;

  public RssFeedItemDetailPage(
      IRssFeedItemDetailService service,
      IServiceProvider serviceProvider,
      string itemId,
      NewsFeedItemKind kind,
      IUiLocalization localization,
      IUiLocaleController localeController)
  {
    this.serviceProvider = serviceProvider;
    sessionStore = serviceProvider.GetRequiredService<ISessionStore>();
    viewModel = new(service, itemId, kind, localization, localeController);
    hnDiscussions = new(
        serviceProvider.GetRequiredService<IHnDiscussionsSettings>(),
        serviceProvider.GetRequiredService<HnDiscussionsClient>(),
        localization);
    BindingContext = viewModel;
    Title = UiCopy.Localize(UiMessageKey.NativeTaxonomyListsRssFeedItem);
    sessionChangedHandler = (_, _) => MainThread.BeginInvokeOnMainThread(RefreshFollowerSendVisibility);

    var image = new Image { HeightRequest = 220, Aspect = Aspect.AspectFill };
    image.SetBinding(Image.SourceProperty, "Detail.Item.ThumbnailUrl");
    var source = BoundLabel(nameof(RssFeedItemDetailViewModel.UserContentSource), 13, FontAttributes.Bold);
    var title = BoundLabel(nameof(RssFeedItemDetailViewModel.UserContentTitle), 24, FontAttributes.Bold);
    var summary = new NativeHtmlContentView();
    summary.SetBinding(NativeHtmlContentView.HtmlProperty, "Detail.ContentHtml");
    summary.SetBinding(NativeHtmlContentView.FallbackProperty, nameof(RssFeedItemDetailViewModel.ExternalContentSummary));
    var published = BoundLabel(nameof(RssFeedItemDetailViewModel.LocalizedPublishedAt), 13);
    var votes = BoundLabel(nameof(RssFeedItemDetailViewModel.LocalizedVoteCounts), 13);
    var bookmarks = BoundLabel(nameof(RssFeedItemDetailViewModel.LocalizedSavedState), 13);
    var hidden = BoundLabel(nameof(RssFeedItemDetailViewModel.LocalizedHiddenState), 13);
    var error = BoundLabel(nameof(RssFeedItemDetailViewModel.ErrorMessage), 14);
    error.TextColor = Colors.IndianRed;

    var retry = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeSwiftCommonTryAgain);
    retry.SetBinding(IsVisibleProperty, nameof(RssFeedItemDetailViewModel.HasError));
    retry.Clicked += async (_, _) => await viewModel.LoadAsync().ConfigureAwait(true);
    var play = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetMediaPlaybackPlay);
    play.SetBinding(IsVisibleProperty, "Detail.Item.HasMediaPlayback");
    play.Clicked += OnPlayClicked;
    var videoUnavailable = UiCopy.Bind(
        new Label { VerticalTextAlignment = TextAlignment.Center, FontSize = 13 },
        Label.TextProperty,
        UiMessageKey.NativeDotnetMediaPlaybackVideoUnavailable);
    videoUnavailable.SetBinding(IsVisibleProperty, "Detail.Item.IsEmbedOnlyVideo");
    var open = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeSwiftPodcastPlaybackOpenSource);
    open.SetBinding(IsVisibleProperty, "Detail.Item.CanOpenExternally");
    open.Clicked += OnOpenClicked;
    followerSend = UiCopy.Bind(
        new Button(),
        Button.TextProperty,
        new UiMessageKey("native.swift.followerDistribution.actions"));
    followerSend.Clicked += OnFollowerSendClicked;
    RefreshFollowerSendVisibility();

    var hnHeading = new Label { FontAttributes = FontAttributes.Bold };
    hnHeading.SetBinding(Label.TextProperty, nameof(HnDiscussionsViewModel.LocalizedHeading));
    hnHeading.BindingContext = hnDiscussions;
    var hnThreads = new CollectionView { SelectionMode = SelectionMode.None };
    hnThreads.SetBinding(ItemsView.ItemsSourceProperty, nameof(HnDiscussionsViewModel.Threads));
    hnThreads.BindingContext = hnDiscussions;
    hnThreads.ItemTemplate = new DataTemplate(() =>
    {
      var title = new Button();
      title.SetBinding(Button.TextProperty, nameof(HnDiscussionRow.Title));
      title.Clicked += OnHnDiscussionClicked;
      var metadata = new Label { FontSize = 13 };
      metadata.SetBinding(Label.TextProperty, nameof(HnDiscussionRow.LocalizedMetadata));
      return new VerticalStackLayout { Spacing = 2, Children = { title, metadata } };
    });
    var hnStack = new VerticalStackLayout { Spacing = 8, Children = { hnHeading, hnThreads } };
    hnStack.SetBinding(IsVisibleProperty, nameof(HnDiscussionsViewModel.HasThreads));
    hnStack.BindingContext = hnDiscussions;

    Content = new ScrollView
    {
      Content = new VerticalStackLayout
      {
        Padding = 20,
        Spacing = 10,
        Children =
        {
          image, source, title, summary, published, votes, bookmarks, hidden,
          new HorizontalStackLayout { Spacing = 8, Children = { play, videoUnavailable, open, followerSend } },
          hnStack,
          error, retry,
        },
      },
    };
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    AttachSessionChanged();
    if (!viewModel.HasDetail) await viewModel.LoadAsync().ConfigureAwait(true);
    RefreshFollowerSendVisibility();
    await hnDiscussions.LoadAsync([viewModel.Detail?.Item.Link?.ToString()]).ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    DetachSessionChanged();
    base.OnDisappearing();
  }

  private async void OnPlayClicked(object? sender, EventArgs args)
  {
    if (viewModel.Detail?.Item is not { HasMediaPlayback: true } item) return;
    await Navigation.PushAsync(new MediaPlaybackPage(
        item,
        serviceProvider.GetRequiredService<VouchaApiClient>(),
        serviceProvider.GetRequiredService<ISessionStore>(),
        serviceProvider.GetRequiredService<IUiLocaleController>())).ConfigureAwait(true);
  }

  private async void OnOpenClicked(object? sender, EventArgs args)
  {
    if (viewModel.Detail?.Item.Link is { } link) await Launcher.Default.OpenAsync(link);
  }

  private async void OnFollowerSendClicked(object? sender, EventArgs args)
  {
    if (viewModel.Detail?.Item is { Id: { } id } && FollowerDistributionActions.CanSendRssItem(sessionStore))
      await FollowerDistributionActions.ShowAsync(this, serviceProvider, sessionStore, FollowerDistributionTargetKind.RssFeedItem, id);
  }

  private async void OnHnDiscussionClicked(object? sender, EventArgs args)
  {
    if (sender is Button { BindingContext: HnDiscussionRow row })
    {
      await Launcher.Default.OpenAsync(row.ItemUrl).ConfigureAwait(true);
    }
  }

  private static Label BoundLabel(
      string path,
      double fontSize,
      FontAttributes attributes = FontAttributes.None,
      string? format = null)
  {
    var label = new Label { FontSize = fontSize, FontAttributes = attributes };
    label.SetBinding(Label.TextProperty, new Binding(path, stringFormat: format));
    return label;
  }

  private void RefreshFollowerSendVisibility() =>
      followerSend.IsVisible =
          viewModel.Detail?.Item.HasItemActions == true && FollowerDistributionActions.CanSendRssItem(sessionStore);

  private void AttachSessionChanged()
  {
    if (isSessionChangedAttached) return;
    sessionStore.SessionChanged += sessionChangedHandler;
    isSessionChangedAttached = true;
  }

  private void DetachSessionChanged()
  {
    if (!isSessionChangedAttached) return;
    sessionStore.SessionChanged -= sessionChangedHandler;
    isSessionChangedAttached = false;
  }
}

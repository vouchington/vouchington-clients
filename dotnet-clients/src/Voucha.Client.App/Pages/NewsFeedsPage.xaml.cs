using Voucha.Client.Core.Auth;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class NewsFeedsPage : ContentPage
{
  private readonly NewsFeedsViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly IServiceProvider serviceProvider;
  private readonly EmailVerificationRecoveryCoordinator emailRecovery;

  public NewsFeedsPage(
      NewsFeedsViewModel viewModel,
      ISessionStore sessionStore,
      IServiceProvider serviceProvider,
      EmailVerificationRecoveryCoordinator emailRecovery)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    this.serviceProvider = serviceProvider;
    this.emailRecovery = emailRecovery;
    BindingContext = new NewsFeedsPageBinding(viewModel, sessionStore);
  }

  public Task SelectScopeAsync(NewsFeedScope scope) => viewModel.SelectScopeAsync(scope);

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (BindingContext is NewsFeedsPageBinding binding) binding.AttachSessionChanged();
    await viewModel.LoadAsync();
  }

  protected override void OnDisappearing()
  {
    if (BindingContext is NewsFeedsPageBinding binding) binding.DetachSessionChanged();
    base.OnDisappearing();
  }

  private async void OnYourFeedClicked(object? sender, EventArgs e)
  {
    await viewModel.SelectScopeAsync(GetPrimaryScope());
  }

  private async void OnAllNewsClicked(object? sender, EventArgs e)
  {
    await viewModel.SelectScopeAsync(GetAllItemsScope());
  }

  private async void OnYourSourcesClicked(object? sender, EventArgs e)
  {
    await viewModel.SelectScopeAsync(GetSourcesScope());
  }

  private async void OnAllSourcesClicked(object? sender, EventArgs e)
  {
    await viewModel.SelectScopeAsync(GetAllSourcesScope());
  }

  private async void OnArticleSourceFeedTypeClicked(object? sender, EventArgs e)
  {
    await viewModel.SelectSourceFeedTypeAsync(NewsFeedSourceType.Article);
  }

  private async void OnPodcastSourceFeedTypeClicked(object? sender, EventArgs e)
  {
    await viewModel.SelectSourceFeedTypeAsync(NewsFeedSourceType.Podcast);
  }

  private async void OnVideoSourceFeedTypeClicked(object? sender, EventArgs e)
  {
    await viewModel.SelectSourceFeedTypeAsync(NewsFeedSourceType.Video);
  }

  private async void OnToggleSourceClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: NewsFeedItem item })
    {
      await viewModel.ToggleSourceFollowAsync(item);
    }
  }

  private async void OnToggleSourceMuteClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: NewsFeedItem item })
    {
      await viewModel.ToggleSourceMuteAsync(item);
    }
  }

  private async void OnToggleTopicClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: NewsFeedItem item })
    {
      await viewModel.ToggleTopicFollowAsync(item);
    }
  }

  private async void OnToggleTopicMuteClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: NewsFeedItem item })
    {
      await viewModel.ToggleTopicMuteAsync(item);
    }
  }

  private async void OnSaveClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: NewsFeedItem item })
    {
      await viewModel.ToggleSaveAsync(item);
    }
  }

  private async void OnHideClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: NewsFeedItem item })
    {
      await viewModel.ToggleHideAsync(item);
    }
  }

  private async void OnToggleReadClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: NewsFeedItem item })
    {
      await viewModel.ToggleReadAsync(item);
    }
  }

  private async void OnChooseVoteClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: NewsFeedItem item })
    {
      if (!sessionStore.Current.CanCastPublicVotes()) return;
      var choice = await SemanticVoteActionSheet.ChooseSentimentAsync(this, item.CurrentVoteChoice);
      if (choice is null) return;
      await viewModel.VoteRssFeedItemAsync(item, choice);
      await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
    }
  }

  private async void OnClearVoteClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: NewsFeedItem item })
    {
      if (!sessionStore.Current.CanClearPublicVote(item.CurrentVoteChoice)) return;
      await viewModel.VoteRssFeedItemAsync(item, null);
      await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
    }
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Reliability",
      "CA2000:Dispose objects before losing scope",
      Justification = "MediaPlaybackPage owns its locale subscription and persistence gate and disposes them when MAUI unloads the page.")]
  private async void OnPlayClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: NewsFeedItem item } || !item.HasMediaPlayback)
    {
      return;
    }

    var page = new MediaPlaybackPage(
        item,
        serviceProvider.GetRequiredService<VouchaApiClient>(),
        sessionStore,
        serviceProvider.GetRequiredService<IUiLocaleController>());
    await Navigation.PushAsync(page);
  }

  private async void OnOpenClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: NewsFeedItem item } || item.Link is null)
    {
      return;
    }

    await Launcher.Default.OpenAsync(item.Link);
  }

  private NewsFeedScope GetPrimaryScope() =>
      sessionStore.Current.IsAuthenticated
          ? viewModel.FeedKind.GetAuthenticatedScope()
          : viewModel.FeedKind.GetAnonymousScope();

  private NewsFeedScope GetAllItemsScope() =>
      viewModel.FeedKind.GetAnonymousScope();

  private NewsFeedScope GetSourcesScope() => viewModel.FeedKind.GetSourcesScope();

  private NewsFeedScope GetAllSourcesScope() => viewModel.FeedKind.GetAllSourcesScope();
}

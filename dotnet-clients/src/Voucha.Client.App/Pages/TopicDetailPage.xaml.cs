using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core;
using Voucha.Client.Core.Topics;
using Voucha.Client.App.Controls;

namespace Voucha.Client.App.Pages;

public partial class TopicDetailPage : ContentPage
{
  private readonly TopicDetailViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private readonly string topicIdOrSlug;
  private readonly EmailVerificationRecoveryCoordinator emailRecovery;
  private readonly bool showSourceCrawls;
  private readonly bool isSourceDetail;
  private readonly AppConfig config;
  private readonly string? sourceCrawlId;
  private bool lastStaffViewer;
  private bool isVisible;
  private bool isTopicChangeAttached;

  public TopicDetailPage(
      TopicDetailViewModel viewModel,
      ISessionStore sessionStore,
      string topicIdOrSlug,
      EmailVerificationRecoveryCoordinator emailRecovery,
      AppConfig config,
      bool showSourceCrawls = false,
      string? sourceCrawlId = null,
      bool isSourceDetail = false)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    sessionChangedHandler = (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = RefreshAfterSessionChangedAsync());
    this.topicIdOrSlug = topicIdOrSlug;
    this.emailRecovery = emailRecovery ?? throw new ArgumentNullException(nameof(emailRecovery));
    this.config = config ?? throw new ArgumentNullException(nameof(config));
    this.showSourceCrawls = showSourceCrawls;
    this.sourceCrawlId = sourceCrawlId;
    this.isSourceDetail = isSourceDetail;
    lastStaffViewer = IsStaffViewer;
    BindingContext = viewModel;
    AttachTopicChanges();
    RefreshTopicLogo();
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI lifecycle handlers must not throw.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    isVisible = true;
    sessionStore.SessionChanged += sessionChangedHandler;
    AttachTopicChanges();
    RefreshViewerState();
    lastStaffViewer = IsStaffViewer;
    RefreshTopicLogo();
    try
    {
      await viewModel.LoadAsync(topicIdOrSlug, followSource: true, isSourceDetail: isSourceDetail).ConfigureAwait(true);
      if (sourceCrawlId is not null) await viewModel.LoadSourceCrawlAsync(sourceCrawlId).ConfigureAwait(true);
      else if (showSourceCrawls) await viewModel.LoadSourceCrawlsAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  protected override void OnDisappearing()
  {
    isVisible = false;
    sessionStore.SessionChanged -= sessionChangedHandler;
    if (isTopicChangeAttached)
    {
      viewModel.PropertyChanged -= OnViewModelPropertyChanged;
      isTopicChangeAttached = false;
    }
    base.OnDisappearing();
  }

  private bool IsStaffViewer => sessionStore.Current.Identity?.Roles?.Any(role =>
      role is "administrator" or "moderator") == true;

  private void RefreshViewerState() => viewModel.IsStaffViewer = IsStaffViewer;

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI session handlers must not throw.")]
  internal async Task RefreshAfterSessionChangedAsync()
  {
    var isStaffViewer = IsStaffViewer;
    var gainedStaffAccess = isStaffViewer && !lastStaffViewer;
    lastStaffViewer = isStaffViewer;
    RefreshViewerState();
    if (!isVisible || !gainedStaffAccess) return;
    try
    {
      await viewModel.LoadAsync(topicIdOrSlug, followSource: true, isSourceDetail: isSourceDetail).ConfigureAwait(true);
      if (sourceCrawlId is not null) await viewModel.LoadSourceCrawlAsync(sourceCrawlId).ConfigureAwait(true);
      else if (showSourceCrawls) await viewModel.LoadSourceCrawlsAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(viewModel.Topic)) RefreshTopicLogo();
  }

  private void AttachTopicChanges()
  {
    if (isTopicChangeAttached) return;
    viewModel.PropertyChanged += OnViewModelPropertyChanged;
    isTopicChangeAttached = true;
  }

  private void RefreshTopicLogo()
  {
    var placement = viewModel.Topic?.LogoImagePlacement;
    var url = placement is null
        ? null
        : config.ImageUrlForPlacement(placement.PlacementId, placement.PlacementRevision, placement.ImageId, 320);
    TopicLogoImage.Source = url is null ? null : ImageSource.FromUri(url);
    TopicLogoImage.IsVisible = url is not null;
  }

  private async void OnChooseVoteClicked(object? sender, EventArgs e)
  {
    if (!sessionStore.Current.CanCastPublicVotes()) return;
    var choice = await SemanticVoteActionSheet.ChooseSentimentAsync(this, viewModel.CurrentVoteChoice);
    if (choice is null) return;
    await viewModel.VoteTopicAsync(choice);
    await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
  }

  private async void OnClearVoteClicked(object? sender, EventArgs e)
  {
    if (!sessionStore.Current.CanClearPublicVote(viewModel.CurrentVoteChoice)) return;
    await viewModel.VoteTopicAsync(null);
    await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
  }

  private async void OnToggleTopicFollowClicked(object? sender, EventArgs e)
  {
    if (sessionStore.Current.IsAuthenticated) await viewModel.ToggleTopicFollowAsync();
  }

  private async void OnToggleTopicMuteClicked(object? sender, EventArgs e)
  {
    if (sessionStore.Current.IsAuthenticated) await viewModel.ToggleTopicMuteAsync();
  }

  private async void OnToggleSourceFollowClicked(object? sender, EventArgs e)
  {
    if (sessionStore.Current.IsAuthenticated) await viewModel.ToggleSourceFollowAsync();
  }

  private async void OnToggleSourceMuteClicked(object? sender, EventArgs e)
  {
    if (sessionStore.Current.IsAuthenticated) await viewModel.ToggleSourceMuteAsync();
  }

  private async void OnShowSourceCrawlsClicked(object? sender, EventArgs e)
  {
    try
    {
      await viewModel.LoadSourceCrawlsAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async void OnLoadMoreSourceCrawlsClicked(object? sender, EventArgs e)
  {
    try
    {
      await viewModel.LoadMoreSourceCrawlsAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async void OnSourceCrawlTapped(object? sender, TappedEventArgs e)
  {
    if (e.Parameter is not string crawlId) return;
    await Navigation.PushAsync(new TopicDetailPage(
        viewModel.CreateSourceCrawlDetailViewModel(),
        sessionStore,
        topicIdOrSlug,
        emailRecovery,
        showSourceCrawls: true,
        sourceCrawlId: crawlId,
        isSourceDetail: isSourceDetail,
        config: config));
  }

  private async void OnRetrySourceCrawlHistoryClicked(object? sender, EventArgs e)
  {
    await viewModel.RetrySourceCrawlHistoryAsync().ConfigureAwait(true);
  }
}

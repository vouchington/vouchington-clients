using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Topics;
using Voucha.Client.App.Controls;

namespace Voucha.Client.App.Pages;

public partial class TopicDetailPage : ContentPage
{
  private readonly TopicDetailViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly string topicIdOrSlug;
  private readonly EmailVerificationRecoveryCoordinator emailRecovery;
  private readonly bool showSourceCrawls;
  private readonly string? sourceCrawlId;

  public TopicDetailPage(
      TopicDetailViewModel viewModel,
      ISessionStore sessionStore,
      string topicIdOrSlug,
      EmailVerificationRecoveryCoordinator emailRecovery,
      bool showSourceCrawls = false,
      string? sourceCrawlId = null)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    this.topicIdOrSlug = topicIdOrSlug;
    this.emailRecovery = emailRecovery ?? throw new ArgumentNullException(nameof(emailRecovery));
    this.showSourceCrawls = showSourceCrawls;
    this.sourceCrawlId = sourceCrawlId;
    BindingContext = viewModel;
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI lifecycle handlers must not throw.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await viewModel.LoadAsync(topicIdOrSlug).ConfigureAwait(true);
      if (sourceCrawlId is not null) await viewModel.LoadSourceCrawlAsync(sourceCrawlId).ConfigureAwait(true);
      else if (showSourceCrawls) await viewModel.LoadSourceCrawlsAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
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
        sourceCrawlId: crawlId));
  }

  private async void OnRetrySourceCrawlHistoryClicked(object? sender, EventArgs e)
  {
    await viewModel.RetrySourceCrawlHistoryAsync().ConfigureAwait(true);
  }
}

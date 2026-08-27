using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Topics;
using Voucha.Client.Core.Localization;
using Voucha.Client.App.Controls;

namespace Voucha.Client.App.Pages;

public partial class TopicsPage : ContentPage
{
  private readonly TopicsViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly IBookmarkService bookmarkService;
  private readonly ITopicsService topicsService;
  private readonly IImageUploadService imageUploadService;
  private readonly VouchaApiClient apiClient;
  private readonly EmailVerificationRecoveryCoordinator emailRecovery;
  private readonly IServiceProvider serviceProvider;
  private readonly IUiLocalization localization;
  private readonly IUiLocaleController localeController;

  public TopicsPage(
      TopicsViewModel viewModel,
      ISessionStore sessionStore,
      IBookmarkService bookmarkService,
      ITopicsService topicsService,
      IImageUploadService imageUploadService,
      VouchaApiClient apiClient,
      EmailVerificationRecoveryCoordinator emailRecovery,
      IServiceProvider serviceProvider,
      IUiLocalization localization,
      IUiLocaleController localeController)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    this.bookmarkService = bookmarkService;
    this.topicsService = topicsService;
    this.imageUploadService = imageUploadService;
    this.apiClient = apiClient;
    this.emailRecovery = emailRecovery;
    this.serviceProvider = serviceProvider;
    this.localization = localization;
    this.localeController = localeController;
    BindingContext = new TopicsPageBinding(viewModel, sessionStore);
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void lifecycle methods must not allow load failures to escape to the dispatcher.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (BindingContext is TopicsPageBinding binding) binding.AttachSessionChanged();
    try
    {
      await viewModel.LoadAsync();
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  protected override void OnDisappearing()
  {
    if (BindingContext is TopicsPageBinding binding) binding.DetachSessionChanged();
    base.OnDisappearing();
  }

  private async void OnSearchButtonPressed(object? sender, EventArgs e)
  {
    await viewModel.LoadAsync();
  }

  private async void OnChooseVoteClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: TopicRow item })
    {
      if (!sessionStore.Current.CanCastPublicVotes()) return;
      var choice = await SemanticVoteActionSheet.ChooseSentimentAsync(this, item.CurrentVoteChoice);
      if (choice is null) return;
      await viewModel.VoteTopicAsync(item, choice);
      await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
    }
  }

  private async void OnClearVoteClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: TopicRow item })
    {
      if (!sessionStore.Current.CanClearPublicVote(item.CurrentVoteChoice)) return;
      await viewModel.VoteTopicAsync(item, null);
      await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
    }
  }

  private async void OnCreateClicked(object? sender, EventArgs e)
  {
    if (!sessionStore.Current.CanManageTopics()) return;
    await Navigation.PushAsync(new TopicManagementPage(topicsService, imageUploadService, apiClient));
  }

  private async void OnOpenClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: TopicRow item })
    {
      await Navigation.PushAsync(new TopicDetailPage(
          new TopicDetailViewModel(
              topicsService,
              bookmarkService,
              localization,
              localeController,
              apiClient: apiClient,
              canViewSourceCrawlHistory: false,
              canManageSourceCrawls: sessionStore.Current.CanManageTopics(),
              requiresAuthoritativeSourceCrawlMembership: true),
          sessionStore,
          item.Id,
          emailRecovery));
    }
  }

  private async void OnSettingsClicked(object? sender, EventArgs e)
  {
    if (!sessionStore.Current.CanManageTopics()) return;
    if (sender is Button { CommandParameter: TopicRow item })
    {
      await Navigation.PushAsync(new TopicManagementPage(topicsService, imageUploadService, apiClient, item.Id));
    }
  }
}

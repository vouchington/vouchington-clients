using System.ComponentModel;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

[QueryProperty(nameof(Slug), "slug")]
public abstract partial class CommunitySectionPage : ContentPage
{
  private readonly CommunityDetailViewModel viewModel;
  private readonly CommunityDetailSurfaceSection section;
  private readonly Label titleLabel = new() { FontAttributes = FontAttributes.Bold, FontSize = 20 };
  private readonly Label metaLabel = new();
  private readonly Label errorLabel = new() { TextColor = Colors.Red };
  private readonly Button joinButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesJoin);
  private readonly Button leaveButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesLeave);
  private readonly Button archiveButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesArchive);
  private readonly Button unarchiveButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesUnarchive);
  private readonly View actionPanel;
  private readonly CollectionView rowsView = new() { ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems };
  private readonly Button loadMoreButton = UiCopy.Bind(
      new Button(),
      Button.TextProperty,
      UiMessageKey.NativeDotnetDirectMessagesLoadMore);
  private readonly HybridPaginationControl paginationControl;
  private Switch? settingsAllowReviewPostsSwitch;
  private Switch? settingsAllowDataPointPostsSwitch;
  private string? slug;
  private string? initialModmailThreadId;

  protected CommunitySectionPage(
      CommunityDetailViewModel viewModel,
      CommunityDetailSurfaceSection section,
      UiMessageKey title)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.section = section;
    paginationControl = new HybridPaginationControl
    {
      PaginationId = $"community-{section.ToString().ToLowerInvariant()}",
    };
    BindingContext = viewModel;
    SetDynamicResource(TitleProperty, title.Value);
    viewModel.PropertyChanged += OnPropertyChanged;
    joinButton.Clicked += async (_, _) => await MutateAsync(viewModel.JoinAsync).ConfigureAwait(true);
    leaveButton.Clicked += async (_, _) => await MutateAsync(viewModel.LeaveAsync).ConfigureAwait(true);
    archiveButton.Clicked += async (_, _) => await MutateAsync(viewModel.ArchiveAsync).ConfigureAwait(true);
    unarchiveButton.Clicked += async (_, _) => await MutateAsync(viewModel.UnarchiveAsync).ConfigureAwait(true);
    loadMoreButton.Clicked += async (_, _) =>
    {
      await viewModel.LoadMoreCommunityListAsync().ConfigureAwait(true);
      Render();
    };
    paginationControl.LoadNextPageRequested += OnLoadNextPageRequested;
    InitializePendingReportsPagination();
    rowsView.RemainingItemsThreshold = 2;
    rowsView.RemainingItemsThresholdReached += (_, _) =>
    {
      if (viewModel.CanAutomaticallyLoadCommunityList)
        paginationControl.TryLoadAutomatically();
    };
    actionPanel = BuildActionPanel();
    actionPanel.IsVisible = false;
    InitializeTransparencyRangeSelector();
    rowsView.ItemTemplate = new DataTemplate(RowTemplate);
    Content = new ScrollView
    {
      Content = new VerticalStackLayout
      {
        Padding = 16,
        Spacing = 12,
        Children =
        {
          titleLabel,
          metaLabel,
          new HorizontalStackLayout { Spacing = 8, Children = { joinButton, leaveButton, archiveButton, unarchiveButton } },
          actionPanel,
          transparencyRangeSelector,
          errorLabel,
          pendingReportsPanel,
          rowsView,
          paginationControl,
          loadMoreButton,
        },
      },
    };
  }

  public string? Slug { get => slug; set => slug = value; }

  public string? InitialModmailThreadId { get => initialModmailThreadId; set => initialModmailThreadId = value; }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (!string.IsNullOrWhiteSpace(Slug))
    {
      await LoadAsync(Slug).ConfigureAwait(true);
    }
  }

  private async Task LoadAsync(string idOrSlug)
  {
    if (initialRouteState.TryTakeModerationTransparencyRange(out var initialRange))
    {
      viewModel.SetModerationTransparencyRange(initialRange);
    }
    if (section is CommunityDetailSurfaceSection.Moderation or CommunityDetailSurfaceSection.Modmail &&
        !string.IsNullOrWhiteSpace(InitialModmailThreadId))
    {
      await viewModel.LoadModmailThreadSurfaceAsync(idOrSlug, InitialModmailThreadId).ConfigureAwait(true);
    }
    else
    {
      await viewModel.LoadSurfaceAsync(idOrSlug, section).ConfigureAwait(true);
    }
    Render();
  }

  private async Task MutateAsync(Func<CancellationToken, Task<bool>> action)
  {
    await action(CancellationToken.None).ConfigureAwait(true);
    Render();
  }

  private async void OnLoadNextPageRequested(object? sender, EventArgs args)
  {
    await LoadNextPageAsync().ConfigureAwait(true);
    Render();
  }

  private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (AffectsPendingReports(e.PropertyName)) RenderPendingReports();
    if (AffectsRenderedSurface(e.PropertyName))
    {
      Render();
    }
  }

  private static bool AffectsRenderedSurface(string? propertyName) =>
      propertyName is null or nameof(CommunityDetailViewModel.Community) or nameof(CommunityDetailViewModel.State) or
          nameof(CommunityDetailViewModel.ErrorMessage) or nameof(CommunityDetailViewModel.IsArchived) or
          nameof(CommunityDetailViewModel.CanJoin) or nameof(CommunityDetailViewModel.CanLeave) or
          nameof(CommunityDetailViewModel.CanArchive) or nameof(CommunityDetailViewModel.CanUnarchive) or
          nameof(CommunityDetailViewModel.CanModerateCommunity) or nameof(CommunityDetailViewModel.CanManageCommunity) or
          nameof(CommunityDetailViewModel.CanManageMembers) or
          nameof(CommunityDetailViewModel.CanUseModmail) or
          nameof(CommunityDetailViewModel.CanViewRawModerationAnalytics) or
          nameof(CommunityDetailViewModel.CanLoadMoreCommunityList) or
          nameof(CommunityDetailViewModel.CommunityListPaginationErrorMessage) or
          nameof(CommunityDetailViewModel.TransparencyPaginationError) or
          nameof(CommunityDetailViewModel.Members) or
          nameof(CommunityDetailViewModel.Posts) or nameof(CommunityDetailViewModel.ListItemCounts) or
          nameof(CommunityDetailViewModel.News) or nameof(CommunityDetailViewModel.PinnedPosts) or
          nameof(CommunityDetailViewModel.Applications) or nameof(CommunityDetailViewModel.Invites) or
          nameof(CommunityDetailViewModel.Moderation);
  private void Render()
  {
    RefreshLocalizedPickers();
    titleLabel.Text = viewModel.Community?.Name ?? Title;
    metaLabel.Text = viewModel.Community is null
        ? UiCopy.Localize(UiMessageKey.NativeDotnetResidualCommunity)
        : viewModel.IsArchived
            ? UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCommunitiesArchived)
            : viewModel.Community.Visibility;
    errorLabel.Text = PaginationErrorMessage();
    errorLabel.IsVisible = CommunityModerationTransparencyControls.ShouldShowErrorLabel(
        viewModel.HasError, viewModel.HasCommunityListPaginationError, viewModel.TransparencyPaginationError);
    joinButton.IsVisible = viewModel.CanJoin;
    leaveButton.IsVisible = viewModel.CanLeave;
    archiveButton.IsVisible = viewModel.CanArchive;
    unarchiveButton.IsVisible = viewModel.CanUnarchive;
    actionPanel.IsVisible = CanShowActionPanel();
    RenderTransparencyRangeSelector();
    UpdateSettingsSwitches();
    rowsView.ItemsSource = Rows();
    RenderPendingReports();
    ConfigurePaginationControl();
    loadMoreButton.IsVisible = viewModel.HasMoreCommunityList &&
        !viewModel.CanAutomaticallyLoadCommunityList;
    loadMoreButton.IsEnabled = viewModel.CanLoadMoreCommunityList;
  }

  private bool CanShowActionPanel() =>
      section switch
      {
        CommunityDetailSurfaceSection.Settings => viewModel.CanModerateCommunity,
        CommunityDetailSurfaceSection.Lists or CommunityDetailSurfaceSection.Applications or
            CommunityDetailSurfaceSection.Invites or CommunityDetailSurfaceSection.PinnedPosts or
            CommunityDetailSurfaceSection.Bans or CommunityDetailSurfaceSection.Restrictions or
            CommunityDetailSurfaceSection.ModeratorVacation or CommunityDetailSurfaceSection.AiAgents or
            CommunityDetailSurfaceSection.AgentPrompts or CommunityDetailSurfaceSection.Moderation or
            CommunityDetailSurfaceSection.ModerationAnalytics => viewModel.CanModerateCommunity,
        CommunityDetailSurfaceSection.Members => viewModel.CanManageMembers,
        CommunityDetailSurfaceSection.Modmail => viewModel.CanUseModmail,
        _ => false,
      };
}

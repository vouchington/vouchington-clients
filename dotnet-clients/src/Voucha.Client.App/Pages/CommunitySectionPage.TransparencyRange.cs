using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private readonly HorizontalStackLayout transparencyRangeSelector = new() { Spacing = 6 };
  private Button? transparencyTodayButton;

  private void InitializeTransparencyRangeSelector()
  {
    transparencyRangeSelector.IsVisible = false;
    transparencyTodayButton = AddTransparencyRangeButton(
        UiMessageKey.NativeSwiftCommunityRowsTransparencyLatestReleasedDay,
        ModerationTransparencyRange.Today);
    AddTransparencyRangeButton(UiMessageKey.NativeSwiftGrowthDashboardMessage7d, ModerationTransparencyRange.SevenDays);
    AddTransparencyRangeButton(UiMessageKey.NativeSwiftGrowthDashboardMessage30d, ModerationTransparencyRange.Default);
    AddTransparencyRangeButton(UiMessageKey.NativeSwiftGrowthDashboardMessage90d, ModerationTransparencyRange.NinetyDays);
    AddTransparencyRangeButton(UiMessageKey.NativeDotnetGrowthAll, ModerationTransparencyRange.All);
  }

  private Button AddTransparencyRangeButton(UiMessageKey label, string range)
  {
    var button = UiCopy.Bind(new Button(), Button.TextProperty, label);
    button.AutomationId = $"community-moderation-transparency-range-{range}";
    button.Clicked += async (_, _) =>
    {
      await viewModel.SelectModerationTransparencyRangeAsync(range).ConfigureAwait(true);
      Render();
    };
    transparencyRangeSelector.Children.Add(button);
    return button;
  }

  private void RenderTransparencyRangeSelector()
  {
    transparencyRangeSelector.IsVisible = CommunityModerationTransparencyControls.IsVisibleFor(section);
    transparencyTodayButton?.SetDynamicResource(
        Button.TextProperty,
        CommunityModerationTransparencyControls.TodayLabelFor(viewModel.CanViewRawModerationAnalytics).Value);
  }

  private Task LoadNextPageAsync() => IsTransparencyAnalytics()
      ? viewModel.LoadMoreTransparencyAsync()
      : viewModel.LoadMoreCommunityListAsync();

  private string PaginationErrorMessage() => IsTransparencyAnalytics()
      ? viewModel.TransparencyPaginationError ?? viewModel.ErrorMessage ?? string.Empty
      : viewModel.CommunityListPaginationErrorMessage ?? viewModel.ErrorMessage ?? string.Empty;

  private void ConfigurePaginationControl()
  {
    paginationControl.HasMore = IsTransparencyAnalytics() ? viewModel.CanLoadMoreTransparency : viewModel.CanAutomaticallyLoadCommunityList;
    paginationControl.IsLoading = IsTransparencyAnalytics() ? viewModel.IsLoadingMoreTransparency : viewModel.IsLoadingMoreCommunityList;
    paginationControl.HasError = IsTransparencyAnalytics() ? viewModel.TransparencyPaginationError is not null : viewModel.HasCommunityListPaginationError;
  }

  private bool IsTransparencyAnalytics() => CommunityModerationTransparencyControls.IsVisibleFor(section);
}

using Voucha.Client.Core.Api;
using Voucha.Client.Core.Growth;

namespace Voucha.Client.App.Pages;

public partial class GrowthDashboardPage : ContentPage
{
  private readonly GrowthDashboardViewModel viewModel;
  private bool skipNextAppearingLoad;

  public GrowthDashboardPage(
      GrowthDashboardViewModel viewModel,
      GrowthMetricsRange initialRange = default)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    viewModel.ApplyInitialRange(initialRange);
    BindingContext = viewModel;
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (skipNextAppearingLoad)
    {
      skipNextAppearingLoad = false;
      return;
    }

    await SafeExecuteAsync(() => viewModel.LoadAsync());
  }

  public async Task ApplyRouteRangeAsync(GrowthMetricsRange range)
  {
    await viewModel.SelectRangeAsync(range).ConfigureAwait(true);
    skipNextAppearingLoad = true;
  }

  private async Task SafeExecuteAsync(Func<Task> action)
  {
    try
    {
      await action().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      viewModel.ReportUnexpectedError(ex);
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async void OnRefresh(object? sender, EventArgs e) => await SafeExecuteAsync(() => viewModel.LoadAsync());
  private async void OnTodayClicked(object? sender, EventArgs e) => await SafeExecuteAsync(() => viewModel.SelectRangeAsync(GrowthMetricsRange.Today));
  private async void OnSevenDaysClicked(object? sender, EventArgs e) => await SafeExecuteAsync(() => viewModel.SelectRangeAsync(GrowthMetricsRange.SevenDays));
  private async void OnThirtyDaysClicked(object? sender, EventArgs e) => await SafeExecuteAsync(() => viewModel.SelectRangeAsync(GrowthMetricsRange.ThirtyDays));
  private async void OnNinetyDaysClicked(object? sender, EventArgs e) => await SafeExecuteAsync(() => viewModel.SelectRangeAsync(GrowthMetricsRange.NinetyDays));
  private async void OnAllClicked(object? sender, EventArgs e) => await SafeExecuteAsync(() => viewModel.SelectRangeAsync(GrowthMetricsRange.All));
}

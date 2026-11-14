using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App.Pages;

public partial class EngineeringPostgreSqlPage : ContentPage, IDisposable
{
  private readonly EngineeringPostgreSqlViewModel viewModel;

  public EngineeringPostgreSqlPage(EngineeringPostgreSqlViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    Unloaded += OnUnloaded;
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await PageLoadGuard.RunAsync(() => viewModel.LoadAsync()).ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    viewModel.CancelRangeOperation();
    base.OnDisappearing();
  }

  private void OnUnloaded(object? sender, EventArgs args) => Dispose();

  public void Dispose() => viewModel.Dispose();

  private async void OnRefreshClicked(object? sender, EventArgs e) => await viewModel.LoadAsync().ConfigureAwait(true);
  private async void OnRunMigrationsClicked(object? sender, EventArgs e) => await viewModel.RunMigrationsAsync().ConfigureAwait(true);
  private async void OnRunViewsClicked(object? sender, EventArgs e) => await viewModel.RunViewsAsync().ConfigureAwait(true);
  private async void OnRunConfigDrivenClicked(object? sender, EventArgs e) => await viewModel.RunConfigDrivenAsync().ConfigureAwait(true);
  private async void OnCreatePartitionsClicked(object? sender, EventArgs e) => await viewModel.CreatePartitionsAsync().ConfigureAwait(true);
  private async void OnCleanupPartitionsClicked(object? sender, EventArgs e)
  {
    if (!await DisplayAlertAsync(
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsCleanUpPartitions),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsCleanUpPartitionsDescription),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsCleanUp),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel)).ConfigureAwait(true))
    {
      return;
    }

    await viewModel.CleanupPartitionsAsync().ConfigureAwait(true);
  }
  private async void OnClassifierRangeCreateClicked(object? sender, EventArgs e) => await RunRangeAsync(false, false).ConfigureAwait(true);
  private async void OnClassifierRangeRetireClicked(object? sender, EventArgs e) => await RunRangeAsync(false, true).ConfigureAwait(true);
  private async void OnRequestRangeCreateClicked(object? sender, EventArgs e) => await RunRangeAsync(true, false).ConfigureAwait(true);
  private async void OnRequestRangeRetireClicked(object? sender, EventArgs e) => await RunRangeAsync(true, true).ConfigureAwait(true);

  private Task RunRangeAsync(bool requestFamily, bool retire) =>
      viewModel.ConfirmRangeAsync(requestFamily, retire, body => DisplayAlertAsync(
          viewModel.RangeConfirmationTitle(body),
          viewModel.RangeConfirmationMessage(body),
          viewModel.RangeConfirmationTitle(body),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel)));

  private async void OnTriggerArticleSyncClicked(object? sender, EventArgs e) => await viewModel.TriggerArticleSyncAsync().ConfigureAwait(true);
  private async void OnRefreshArticleSyncClicked(object? sender, EventArgs e) => await viewModel.RefreshArticleSyncStatusAsync().ConfigureAwait(true);
}

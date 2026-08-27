using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class EngineeringPostgreSqlPage : ContentPage
{
  private readonly EngineeringPostgreSqlViewModel viewModel;

  public EngineeringPostgreSqlPage(EngineeringPostgreSqlViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI lifecycle handler surfaces failures through view state.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await viewModel.LoadAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

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
  private async void OnTriggerArticleSyncClicked(object? sender, EventArgs e) => await viewModel.TriggerArticleSyncAsync().ConfigureAwait(true);
  private async void OnRefreshArticleSyncClicked(object? sender, EventArgs e) => await viewModel.RefreshArticleSyncStatusAsync().ConfigureAwait(true);
}

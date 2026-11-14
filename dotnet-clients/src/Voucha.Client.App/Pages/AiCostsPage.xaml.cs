using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App.Pages;

public partial class AiCostsPage : ContentPage, IDisposable
{
  private readonly AiCostsViewModel viewModel;

  public AiCostsPage(AiCostsViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    Unloaded += (_, _) => Dispose();
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await PageLoadGuard.RunAsync(() => viewModel.EnsureLoadedAsync());
  }

  private async void OnRefreshing(object? sender, EventArgs eventArgs) => await viewModel.RefreshAsync();
  private async void OnLoadMoreRequested(object? sender, EventArgs eventArgs) => await viewModel.LoadMoreAsync();
  private async void OnRetryClicked(object? sender, EventArgs eventArgs) => await viewModel.RetryAsync();
  public void Dispose() => viewModel.Dispose();
}

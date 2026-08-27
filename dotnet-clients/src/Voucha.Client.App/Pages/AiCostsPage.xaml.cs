using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Engineering;

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

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The view model owns presentation failures.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try { await viewModel.EnsureLoadedAsync(); }
    catch (Exception error) { System.Diagnostics.Debug.WriteLine(error); }
  }

  private async void OnRefreshing(object? sender, EventArgs eventArgs) => await viewModel.RefreshAsync();
  private async void OnLoadMoreRequested(object? sender, EventArgs eventArgs) => await viewModel.LoadMoreAsync();
  private async void OnRetryClicked(object? sender, EventArgs eventArgs) => await viewModel.RetryAsync();
  public void Dispose() => viewModel.Dispose();
}

using Voucha.Client.Core.Fediverse;

namespace Voucha.Client.App.Pages;

public partial class FediverseInstancesPage : ContentPage
{
  private readonly FediverseInstancesViewModel viewModel;
  private string? initialQuery;

  public FediverseInstancesPage(FediverseInstancesViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    BindingContext = viewModel;
  }

  internal void SetInitialQuery(string? query)
  {
    initialQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
    Query.Text = initialQuery;
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (viewModel.Items.Count == 0) await viewModel.LoadAsync(initialQuery);
  }

  private async void OnSearch(object? sender, EventArgs e)
  {
    initialQuery = Query.Text;
    await viewModel.LoadAsync(initialQuery);
  }

  private async void OnLoadMore(object? sender, EventArgs e) =>
      await viewModel.LoadMoreAsync();

  private async void OnOpen(object? sender, EventArgs e)
  {
    if (sender is Button { BindingContext: FediverseInstanceRow row } && Shell.Current is AppShell shell)
    {
      await shell.OpenNativePathAsync($"/instance/{Uri.EscapeDataString(row.Slug)}");
    }
  }
}

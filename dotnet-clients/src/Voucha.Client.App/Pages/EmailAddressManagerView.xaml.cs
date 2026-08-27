using Voucha.Client.Core.Settings;

namespace Voucha.Client.App.Pages;

public partial class EmailAddressManagerView : ContentView
{
  public EmailAddressManagerView() => InitializeComponent();

  private async void OnLoadMoreRequested(object? sender, EventArgs e)
  {
    if (BindingContext is EmailAddressManagerViewModel viewModel)
    {
      await viewModel.LoadMoreEmailAddressesAsync().ConfigureAwait(true);
    }
  }

  private async void OnRequestClicked(object? sender, EventArgs e)
  {
    if (BindingContext is EmailAddressManagerViewModel viewModel)
    {
      await viewModel.RequestVerificationAsync();
    }
  }

  private async void OnVerifyClicked(object? sender, EventArgs e)
  {
    if (BindingContext is EmailAddressManagerViewModel viewModel)
    {
      await viewModel.VerifyAsync();
    }
  }
}

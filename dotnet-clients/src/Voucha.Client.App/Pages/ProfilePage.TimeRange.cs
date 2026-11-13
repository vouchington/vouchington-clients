namespace Voucha.Client.App.Pages;

public partial class ProfilePage
{
  private async void OnTimeRangeSelected(object? sender, string value) =>
      await viewModel.SelectTimeRangeAsync(value).ConfigureAwait(true);
}

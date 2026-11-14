using Voucha.Client.Core.NewsFeeds;

namespace Voucha.Client.App.Pages;

public sealed partial class NewsFeedsPageBinding
{
  public string HackerNewsFilterLabel => viewModel.HackerNewsFilterLabel;

  public bool IsHackerNewsFilterSelected => viewModel.IsHackerNewsFilterSelected;

  public bool IsHackerNewsFilterVisible => viewModel.IsHackerNewsFilterVisible;

  private void ForwardHackerNewsProperties(string? propertyName)
  {
    if (string.IsNullOrEmpty(propertyName) || propertyName == nameof(NewsFeedsViewModel.HackerNewsFilterLabel))
      OnPropertyChanged(nameof(HackerNewsFilterLabel));
    if (string.IsNullOrEmpty(propertyName) || propertyName == nameof(NewsFeedsViewModel.IsHackerNewsFilterSelected))
      OnPropertyChanged(nameof(IsHackerNewsFilterSelected));
    if (string.IsNullOrEmpty(propertyName) || propertyName == nameof(NewsFeedsViewModel.IsHackerNewsFilterVisible))
      OnPropertyChanged(nameof(IsHackerNewsFilterVisible));
  }
}

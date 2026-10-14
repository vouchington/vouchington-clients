using System.ComponentModel;
using Voucha.Client.Core;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.App.Pages;

public partial class TopicDetailPage
{
  private bool isTopicChangeAttached;

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(viewModel.Topic)) RefreshTopicLogo();
  }

  private void AttachTopicChanges()
  {
    if (isTopicChangeAttached) return;
    viewModel.PropertyChanged += OnViewModelPropertyChanged;
    isTopicChangeAttached = true;
  }

  private void RefreshTopicLogo()
  {
    var placement = viewModel.Topic?.LogoImagePlacement;
    var url = placement is null
        ? null
        : config.ImageUrlForPlacement(placement.PlacementId, placement.PlacementRevision, placement.ImageId, 320);
    TopicLogoImage.Source = url is null ? null : ImageSource.FromUri(url);
    TopicLogoImage.IsVisible = url is not null;
  }
}

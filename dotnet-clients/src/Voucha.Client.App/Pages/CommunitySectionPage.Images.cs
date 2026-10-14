using Voucha.Client.Core;
using Voucha.Client.Core.Api;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private readonly VerticalStackLayout communityImages = new()
  {
    Spacing = 8,
    Children =
    {
      new Image
      {
        HeightRequest = 144,
        Aspect = Aspect.AspectFill,
        AutomationId = "community-banner-image",
      },
      new Image
      {
        WidthRequest = 56,
        HeightRequest = 56,
        Aspect = Aspect.AspectFill,
        AutomationId = "community-profile-image",
      },
    },
  };

  private readonly AppConfig config;

  private View CommunityImagesView() => communityImages;

  private void RenderCommunityImages()
  {
    var community = viewModel.Community;
    var images = communityImages.Children.OfType<Image>().ToArray();
    SetImage(images[0], community?.BannerImagePlacement, 960);
    SetImage(images[1], community?.ProfileImagePlacement, 96);
    AutomationProperties.SetIsInAccessibleTree(images[1], false);
    communityImages.IsVisible = images.Any(image => image.IsVisible);
  }

  private void SetImage(Image image, TopicImagePlacement? placement, int width)
  {
    var url = placement is null
        ? null
        : config.ImageUrlForPlacement(placement.PlacementId, placement.PlacementRevision, placement.ImageId, width);
    image.Source = url is null ? null : ImageSource.FromUri(url);
    image.IsVisible = url is not null;
  }
}

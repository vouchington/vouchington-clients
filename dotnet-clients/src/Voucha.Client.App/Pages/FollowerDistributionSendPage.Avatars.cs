using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using Voucha.Client.Core.Api;

namespace Voucha.Client.App.Pages;

internal sealed partial class FollowerDistributionSendPage
{
  private View Avatar(User user)
  {
    var name = Name(user);
    var placement = user.ProfileImagePlacement;
    var imageUrl = placement is null
        ? null
        : config.ImageUrlForPlacement(placement.PlacementId, placement.PlacementRevision, placement.ImageId, 96);
    var initial = name.Trim().Length == 0 ? "?" : StringInfo.GetNextTextElement(name.Trim()).ToUpperInvariant();
    var initialLabel = new Label
    {
      Text = initial,
      HorizontalOptions = LayoutOptions.Center,
      VerticalOptions = LayoutOptions.Center,
      AutomationId = "follower-recipient-avatar-initial"
    };
    AutomationProperties.SetIsInAccessibleTree(initialLabel, false);
    var image = new Image
    {
      WidthRequest = 40,
      HeightRequest = 40,
      Aspect = Aspect.AspectFill,
      Source = imageUrl is null ? null : ImageSource.FromUri(imageUrl),
      AutomationId = "follower-recipient-avatar-image",
      Clip = new EllipseGeometry { Center = new Point(20, 20), RadiusX = 20, RadiusY = 20 }
    };
    AutomationProperties.SetIsInAccessibleTree(image, false);
    return new Border
    {
      WidthRequest = 40,
      HeightRequest = 40,
      StrokeThickness = 0,
      BackgroundColor = Color.FromArgb("#E0F2FE"),
      StrokeShape = new RoundRectangle { CornerRadius = 20 },
      Content = new Grid { Children = { initialLabel, image } }
    };
  }
}

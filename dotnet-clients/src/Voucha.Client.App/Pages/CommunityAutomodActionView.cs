using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class CommunityAutomodActionView : VerticalStackLayout
{
  private readonly Label actionLabel = new() { AutomationId = "community-automod-action" };

  public CommunityAutomodActionView(string? action)
  {
    Spacing = 3;
    Children.Add(UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetModerationAutomod));
    Children.Add(actionLabel);
    Update(action);
  }

  public void Update(string? action)
  {
    actionLabel.Text = UiCopy.Resolve(UiText.ProtocolValue(action));
    IsVisible = !string.IsNullOrWhiteSpace(action);
  }
}

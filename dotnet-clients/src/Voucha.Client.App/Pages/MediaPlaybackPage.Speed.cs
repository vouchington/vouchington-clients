namespace Voucha.Client.App.Pages;

public partial class MediaPlaybackPage
{
  private void OnSpeed075Clicked(object? sender, EventArgs e) => SetSpeed(0.75);
  private void OnSpeed1Clicked(object? sender, EventArgs e) => SetSpeed(1.0);
  private void OnSpeed125Clicked(object? sender, EventArgs e) => SetSpeed(1.25);
  private void OnSpeed15Clicked(object? sender, EventArgs e) => SetSpeed(1.5);

  private void SetSpeed(double speed)
  {
    if (!item.HasDirectPlayback)
    {
      return;
    }

    Player.Speed = speed;
    StatusLabel.Text = UiCopy.Format(
        Voucha.Client.Core.Localization.UiMessageKey.NativeDotnetCsharpStatusSpeed,
        ("value", speed));
  }
}

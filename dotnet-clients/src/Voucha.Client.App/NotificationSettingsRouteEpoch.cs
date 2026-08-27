namespace Voucha.Client.App;

internal sealed class NotificationSettingsRouteEpoch
{
  private bool notificationRouteFocused;

  public long Value { get; private set; }

  public void Apply(bool focused)
  {
    if (notificationRouteFocused != focused) Value++;
    notificationRouteFocused = focused;
  }

  public void Invalidate() => Value++;
}

namespace Voucha.Client.App;

internal sealed class NotificationSettingsRouteEpoch
{
  private bool notificationRouteFocused;
  private long? activatedEpoch;

  public long Value { get; private set; }

  public void Apply(bool focused)
  {
    if (notificationRouteFocused != focused) Value++;
    notificationRouteFocused = focused;
  }

  public bool TryActivate(long expectedEpoch)
  {
    if (!notificationRouteFocused || expectedEpoch != Value || activatedEpoch == Value) return false;
    activatedEpoch = Value;
    return true;
  }

  public void Invalidate() => Value++;
}

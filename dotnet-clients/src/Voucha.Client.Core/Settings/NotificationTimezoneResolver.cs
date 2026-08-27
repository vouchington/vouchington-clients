namespace Voucha.Client.Core.Settings;

public interface INotificationTimezoneResolver
{
  string? ResolveIanaTimezone();
}

public sealed class SystemNotificationTimezoneResolver : INotificationTimezoneResolver
{
  private static readonly HashSet<string> ianaTimeZoneIds = CreateIanaTimeZoneIds();

  public string? ResolveIanaTimezone()
  {
    var id = TimeZoneInfo.Local.Id;
    if (IsIana(id)) return id;
    return TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId) ? ianaId : null;
  }

  public static bool IsIana(string? value)
  {
    if (string.IsNullOrWhiteSpace(value)) return false;
    if (value == "UTC") return true;
    if (value.Length <= 2 || !value.Contains('/', StringComparison.Ordinal) ||
        value.Any(char.IsWhiteSpace)) return false;
    return ianaTimeZoneIds.Contains(value);
  }

  private static HashSet<string> CreateIanaTimeZoneIds()
  {
    var timeZones = new HashSet<string>(StringComparer.Ordinal);
    foreach (var timeZone in TimeZoneInfo.GetSystemTimeZones())
    {
      if (timeZone.Id.Contains('/', StringComparison.Ordinal)) timeZones.Add(timeZone.Id);
      if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out var ianaId)) timeZones.Add(ianaId);
    }
    return timeZones;
  }
}

namespace Voucha.Client.Core.Tests.Settings;

internal static class SettingsViewModelTestSupport
{
  internal static string DataRequestStatusText(string status) =>
      $"{status} · {DateTimeOffset.Parse("2026-07-01T16:00:00-07:00").ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture)}";
}

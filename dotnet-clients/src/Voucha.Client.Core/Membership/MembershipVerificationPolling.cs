using System.Globalization;

namespace Voucha.Client.Core.NativeBilling;

public static class MembershipVerificationPolling
{
  public static bool IsTerminal(string status, string? reasonCode)
  {
    if (reasonCode == "purchase_pending")
    {
      return false;
    }

    return status is "verified" or "conflict" or "rejected";
  }

  public static TimeSpan Delay(int attempt, string? nextProcessingAt, DateTimeOffset now)
  {
    if (DateTimeOffset.TryParse(
            nextProcessingAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var instant) &&
        instant > now)
    {
      return instant - now;
    }

    TimeSpan[] schedule =
    [
      TimeSpan.FromSeconds(1),
      TimeSpan.FromSeconds(2),
      TimeSpan.FromSeconds(4),
      TimeSpan.FromSeconds(8),
      TimeSpan.FromSeconds(15),
    ];
    var index = Math.Clamp(attempt, 0, schedule.Length - 1);
    return schedule[index];
  }
}

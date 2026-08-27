using System.Diagnostics;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.ReferralLinks;

public enum ReferralLinksMode
{
  Following,
  Mutual,
  Mine,
  Programs,
  Analytics,
}

public sealed record ReferralLinksRouteContext(ReferralLinksMode Mode)
{
  public static ReferralLinksRouteContext FromMatch(NativeRouteMatch match)
  {
    ArgumentNullException.ThrowIfNull(match);

    return new(match.Template switch
    {
      "/feed/referral-links/mutual" => ReferralLinksMode.Mutual,
      "/my/referral-links" => ReferralLinksMode.Mine,
      "/my/referrals" => ReferralLinksMode.Analytics,
      "/referral-programs" => ReferralLinksMode.Programs,
      _ => ReferralLinksMode.Following,
    });
  }
}

public sealed class ReferralLinksRouteContextStore
{
  private object? context;

  public void Set(ReferralLinksRouteContext next)
  {
    ArgumentNullException.ThrowIfNull(next);
    var previous = Interlocked.Exchange(ref context, next);
    if (previous is not null)
    {
      Debug.WriteLine($"ReferralLinksRouteContextStore: overwriting un-consumed context {previous}");
    }
  }

  public ReferralLinksRouteContext? Consume() =>
      (ReferralLinksRouteContext?)Interlocked.Exchange(ref context, null);
}

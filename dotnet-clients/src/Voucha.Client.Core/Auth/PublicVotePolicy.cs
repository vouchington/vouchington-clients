using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

public static class PublicVotePolicy
{
  public static bool CanCastPublicVotes(this SessionSnapshot session)
  {
    ArgumentNullException.ThrowIfNull(session);
    return session.IsAuthenticated &&
        session.Identity?.AccountType is null;
  }

  public static bool CanClearPublicVote(this SessionSnapshot session, ElectionVoteChoice? currentVote)
  {
    ArgumentNullException.ThrowIfNull(session);
    return session.IsAuthenticated &&
        currentVote is not null &&
        !session.CanCastPublicVotes();
  }

  public static bool CanCreateEntityRelationVote(this SessionSnapshot session, bool isUserTag)
  {
    ArgumentNullException.ThrowIfNull(session);
    return session.IsAuthenticated && (!isUserTag || session.CanCastPublicVotes() || session.CanManageTopics());
  }

  public static bool CanManageTopics(this SessionSnapshot session)
  {
    ArgumentNullException.ThrowIfNull(session);
    return session.IsAuthenticated &&
        session.Identity?.Roles is { } roles &&
        roles.Contains("administrator", StringComparer.Ordinal);
  }

  public static bool CanViewPaidCrawlHistory(this SessionSnapshot session)
    => session.CanManageTopics() || session.HasPlusOrProMembershipPlan();

  public static bool CanViewLandingPageAnalytics(this SessionSnapshot session)
    => session.HasPlusOrProMembershipPlan();

  public static bool CanViewLandingPageAnalytics(this Membership? membership)
    => membership.HasActivePlusOrProMembership();

  public static bool HasActivePlusOrProMembership(this Membership? membership)
  {
    if (membership is null ||
        (membership.Status is not "active" and not "past_due") ||
        (membership.ExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow)) return false;
    return string.Equals(membership.Plan, "plus", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(membership.Plan, "pro", StringComparison.OrdinalIgnoreCase);
  }

  private static bool HasPlusOrProMembershipPlan(this SessionSnapshot session)
  {
    ArgumentNullException.ThrowIfNull(session);
    return string.Equals(session.Identity?.MembershipPlan, "plus", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(session.Identity?.MembershipPlan, "pro", StringComparison.OrdinalIgnoreCase);
  }
}

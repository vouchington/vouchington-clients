using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed record CommunityApplicationRow(string Id, string UserId, string Status, string? Message)
{
  public static CommunityApplicationRow FromApplication(CommunityApplication application)
  {
    ArgumentNullException.ThrowIfNull(application);

    var status = application.ApprovedAt is not null ? "approved" :
        application.RejectedAt is not null ? "rejected" : "pending";
    return new(application.Id, application.UserId, status, application.Message);
  }
}

public sealed record CommunityInviteRow(
    string Id,
    string Code,
    UiText RecipientText,
    string Status,
    IUiLocalization Localization)
{
  public string Recipient => Localization.Resolve(RecipientText);

  public static CommunityInviteRow FromInvite(
      CommunityInvite invite,
      IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(invite);

    var recipient = invite.InvitedEmail ?? invite.InvitedUserId;
    var status = invite.RevokedAt is not null ? "revoked" :
        invite.AcceptedAt is not null ? "accepted" :
        invite.DeclinedAt is not null ? "declined" : "pending";
    return new(
        invite.Id,
        invite.Code,
        recipient is null
            ? UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesAnyoneWithCode)
            : UiText.Verbatim(recipient),
        status,
        localization ?? UiLocalization.English);
  }
}

public sealed record CommunityModerationRow(
    string Id, string Target, string Status, string? Reason, AuthoredContentText? TargetContent = null)
{
  public string Title => Target;
  public string Subtitle => Status;
  public string? Detail => Reason;
  public string? AuthoredContent => TargetContent?.Text;
  public string? AuthoredContentFlowDirection => TargetContent is null
      ? null
      : AuthoredContentLanguage.Resolve(
          TargetContent.DeclaredLanguage,
          TargetContent.LinguaRsDetectedLanguage).Direction?.ToString();
  public bool HasAuthoredContent => !string.IsNullOrWhiteSpace(AuthoredContent);

  public static CommunityModerationRow FromQueueEntry(CommunityModerationQueueEntry entry)
  {
    ArgumentNullException.ThrowIfNull(entry);

    return new(entry.Id, entry.TargetLabel ?? entry.EntityId, entry.Status, entry.Reason ?? entry.FlaggedReason, entry.TargetContent);
  }
}

public sealed record CommunityRestrictionRow(string Id, string RestrictionType, string Status, string? Reason)
{
  public static CommunityRestrictionRow FromRestriction(CommunityRestriction restriction)
  {
    ArgumentNullException.ThrowIfNull(restriction);

    return new(
          restriction.Id,
          restriction.RestrictionType,
          restriction.LiftedAt is null ? "active" : "lifted",
          restriction.Reason);
  }
}

public sealed record CommunityBanRow(string Id, string UserId, string Status, string? Reason)
{
  public static CommunityBanRow FromBan(CommunityBan ban)
  {
    ArgumentNullException.ThrowIfNull(ban);

    return new(ban.Id, ban.UserId, ban.LiftedAt is null ? "active" : "lifted", ban.Reason);
  }
}

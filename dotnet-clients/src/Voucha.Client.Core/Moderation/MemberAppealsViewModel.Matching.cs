using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed partial class MemberAppealsViewModel
{
  private static bool Matches(ModerationAppeal appeal, MemberAppealTarget target) =>
      target.TargetType switch
      {
        ModerationAppealTargetType.Warning => appeal.UserWarningId == target.TargetId,
        ModerationAppealTargetType.Ban => appeal.CommunityBanId == target.TargetId,
        ModerationAppealTargetType.Removal =>
            appeal.PostId == target.TargetId && appeal.PostRemovalKind == target.PostRemovalKind,
        ModerationAppealTargetType.Suspension => MatchesSuspension(appeal, target),
        _ => false,
      };

  private static bool MatchesSuspension(
      ModerationAppeal appeal,
      MemberAppealTarget target)
  {
    if (appeal.UserSuspensionId is not { } suspensionId) return false;
    return appeal.TargetContext switch
    {
      ModerationAppealSuspensionContext context =>
        context.Id == suspensionId && context.CreatedAt == target.CreatedAt,
      null => true,
      _ => false,
    };
  }
}

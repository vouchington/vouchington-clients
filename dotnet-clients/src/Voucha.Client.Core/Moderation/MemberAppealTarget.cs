using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed record MemberAppealTarget(
    string Id,
    ModerationAppealTargetType TargetType,
    string? TargetId,
    ModerationAppealPostRemovalKind? PostRemovalKind,
    string? CommunitySlug,
    string? Summary,
    DateTimeOffset CreatedAt)
{
  public static MemberAppealTarget Warning(
      string id,
      string? communitySlug,
      string? summary,
      DateTimeOffset createdAt) =>
      new($"warning:{id}", ModerationAppealTargetType.Warning, id, null,
          communitySlug, summary, createdAt);

  public static MemberAppealTarget Ban(PersonalCommunityBan ban)
  {
    ArgumentNullException.ThrowIfNull(ban);
    return new($"ban:{ban.Id}", ModerationAppealTargetType.Ban, ban.Id, null,
        ban.CommunitySlug, ban.Reason, ban.CreatedAt);
  }

  public static MemberAppealTarget Removal(PersonalRemovedPost post)
  {
    ArgumentNullException.ThrowIfNull(post);
    var kind = string.Equals(post.PostRemovalKind, "platform", StringComparison.OrdinalIgnoreCase)
        ? ModerationAppealPostRemovalKind.Platform
        : ModerationAppealPostRemovalKind.Community;
    var kindId = kind == ModerationAppealPostRemovalKind.Platform ? "platform" : "community";
    return new(
        $"removal:{kindId}:{post.PostId}",
        ModerationAppealTargetType.Removal,
        post.PostId,
        kind,
        post.CommunitySlug,
        post.PostTitle,
        post.UnpublishedAt);
  }

  public static MemberAppealTarget Suspension(DateTimeOffset suspendedAt) =>
      new($"suspension:{suspendedAt.UtcTicks}", ModerationAppealTargetType.Suspension, null, null,
          null, null, suspendedAt);
}

using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed record MemberAppealDraft(
    ModerationAppealReason? Reason = null,
    string Details = "");

public sealed class MemberAppealDraftStore
{
  private readonly Dictionary<string, MemberAppealDraft> drafts = new(StringComparer.Ordinal);

  public MemberAppealDraft Get(string identityId, MemberAppealTarget target)
  {
    ArgumentNullException.ThrowIfNull(target);
    return drafts.GetValueOrDefault(Key(identityId, target)) ?? new();
  }

  public void SetReason(
      string identityId,
      MemberAppealTarget target,
      ModerationAppealReason? reason)
  {
    ArgumentNullException.ThrowIfNull(target);
    var draft = Get(identityId, target);
    drafts[Key(identityId, target)] = draft with { Reason = reason };
  }

  public void SetDetails(string identityId, MemberAppealTarget target, string details)
  {
    ArgumentNullException.ThrowIfNull(target);
    var draft = Get(identityId, target);
    drafts[Key(identityId, target)] = draft with { Details = details };
  }

  public void Clear(string identityId, MemberAppealTarget target)
  {
    ArgumentNullException.ThrowIfNull(target);
    drafts.Remove(Key(identityId, target));
  }

  private static string Key(string identityId, MemberAppealTarget target) =>
      $"{identityId}:{target.Id}";
}

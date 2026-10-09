using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Copyright;

public sealed record CopyrightCaseSnapshot(
    CopyrightNoticeBase Notice,
    DateTimeOffset? AcceptedAt,
    IReadOnlyList<CopyrightNoticeTarget> Targets,
    IReadOnlyList<CopyrightNoticeTimelineEvent> Timeline,
    CopyrightParticipantNotice? Participant)
{
  public static CopyrightCaseSnapshot FromParticipant(CopyrightParticipantNotice notice)
  {
    ArgumentNullException.ThrowIfNull(notice);
    return new(notice, notice.AcceptedAt, notice.Targets, notice.Timeline, notice);
  }

  public static CopyrightCaseSnapshot FromPublic(CopyrightNoticeDetail notice, CopyrightParticipantNotice? participant)
  {
    ArgumentNullException.ThrowIfNull(notice);
    return new(notice, notice.AcceptedAt, notice.Targets, participant?.Timeline ?? notice.Timeline, participant);
  }

}

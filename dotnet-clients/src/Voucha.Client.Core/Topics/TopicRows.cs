using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Topics;

public sealed record TopicRow(
    string Id,
    string Name,
    string Slug,
    string ProtocolTopicType,
    UiText TopicTypeText,
    string? Description,
    double? VoteScoreNet = null,
    int? VoteCountUp = null,
    int? VoteCountDown = null,
    ElectionVoteChoice? CurrentVoteChoice = null,
    IUiLocalization? Localization = null)
{
  public string LocalizedTopicType =>
      (Localization ?? UiLocalization.English).Resolve(TopicTypeText);

  public bool HasVoteCounts => VoteCountUp is not null || VoteCountDown is not null;


  public TopicRow WithLocalization(IUiLocalization localization) =>
      this with { Localization = localization };
}

using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Topics;

public sealed partial class TopicsViewModel
{
  private TopicRow RowFrom(
      Topic topic,
      TopicElection? election = null,
      ElectionVote? vote = null) =>
      new(
          topic.Id,
          topic.Name,
          topic.Slug,
          topic.TopicType,
          UiTaxonomy.TopicType(topic.TopicType),
          topic.Markdown,
          election?.VotesScoreNet,
          election?.VotesCountUp,
          election?.VotesCountDown,
          vote?.Choice,
          localization);
}

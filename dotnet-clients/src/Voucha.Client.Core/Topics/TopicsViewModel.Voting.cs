using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Voting;

namespace Voucha.Client.Core.Topics;

public sealed partial class TopicsViewModel
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native vote mutations surface API failures in view state before MAUI async event handlers observe them.")]
  public async Task VoteTopicAsync(
      TopicRow item,
      ElectionVoteChoice? choice,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (!item.HasVoteCounts) return;
    if (!votingIds.Add(item.Id)) return;

    var previousItems = Items;
    var previousItem = previousItems.FirstOrDefault(row => row.Id == item.Id);
    var mutationLoadRequestId = requestId;
    var optimisticItems = UpdateVote(previousItems, item.Id, choice);
    Items = optimisticItems;
    if (SelectedTopic is not null && SelectedTopic.Id == item.Id)
    {
      SelectedTopic = optimisticItems.FirstOrDefault(row => row.Id == item.Id);
    }
    ErrorMessage = null;
    try
    {
      await EmailVerificationGate.RunAsync(
          () => choice is { } selected
              ? topicsService.VoteTopicAsync(item.Id, selected, cancellationToken)
              : topicsService.ClearTopicVoteAsync(item.Id, cancellationToken),
          ex =>
          {
            RollbackVote(mutationLoadRequestId, item.Id, previousItem, ex.Message);
          }).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      RollbackVote(mutationLoadRequestId, item.Id, previousItem, null);
    }
    catch (Exception ex)
    {
      RollbackVote(mutationLoadRequestId, item.Id, previousItem, ex.Message);
    }
    finally
    {
      votingIds.Remove(item.Id);
    }
  }

  private static TopicRow[] UpdateVote(IReadOnlyList<TopicRow> source, string topicId, ElectionVoteChoice? choice) =>
      source.Select(item => item.Id == topicId ? ApplyVote(item, choice) : item).ToArray();

  private static TopicRow ApplyVote(TopicRow item, ElectionVoteChoice? choice)
  {
    var (nextNet, nextUp, nextDown, nextCurrent) = VoteCalculator.Apply(
        item.CurrentVoteChoice,
        item.VoteScoreNet,
        item.VoteCountUp,
        item.VoteCountDown,
        choice);
    return item with
    {
      VoteScoreNet = nextNet,
      VoteCountUp = nextUp,
      VoteCountDown = nextDown,
      CurrentVoteChoice = nextCurrent,
    };
  }

  private void RollbackVote(
      int mutationLoadRequestId,
      string topicId,
      TopicRow? previousItem,
      string? errorMessage)
  {
    if (requestId != mutationLoadRequestId || previousItem is null)
    {
      return;
    }

    Items = Items.Select(item => item.Id == topicId ? previousItem : item).ToArray();
    if (SelectedTopic is not null && SelectedTopic.Id == topicId)
    {
      SelectedTopic = previousItem;
    }
    ErrorMessage = errorMessage;
  }
}

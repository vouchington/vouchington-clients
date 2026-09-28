using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Voting;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native vote mutations surface API failures in view state before MAUI async event handlers observe them.")]
  public async Task VoteRssFeedItemAsync(NewsFeedItem item, ElectionVoteChoice? choice, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (!item.HasVoteCounts) return;
    if (FindStoryPeer(item.Id) is { } related)
    {
      await MutateStoryPeerAsync(related, item, peer => ApplyVote(peer, choice), false,
          () => SubmitVoteAsync(item, choice, cancellationToken), verifyEmail: true).ConfigureAwait(true);
      return;
    }
    var voteKey = VoteKey(item);
    if (!votingArticleIds.Add(voteKey)) return;

    var previousItems = Items;
    var previousTargetItems = previousItems.Where(row => MatchesVoteTarget(row, item)).ToArray();
    var mutationLoadRequestId = loadRequestId;
    var optimisticItems = UpdateVote(previousItems, item, choice);
    Items = optimisticItems;
    ErrorMessage = null;
    try
    {
      await EmailVerificationGate.RunAsync(
          () => SubmitVoteAsync(item, choice, cancellationToken),
          ex =>
          {
            RollbackVote(mutationLoadRequestId, item, previousTargetItems, ex.Message);
          }).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      RollbackVote(mutationLoadRequestId, item, previousTargetItems, null);
    }
    catch (Exception ex)
    {
      RollbackVote(mutationLoadRequestId, item, previousTargetItems, ex.Message);
    }
    finally
    {
      votingArticleIds.Remove(voteKey);
    }
  }

  private static string VoteKey(NewsFeedItem item) =>
      item.Kind == NewsFeedItemKind.Source && item.TopicId is { } topicId
          ? $"topic:{topicId}"
          : $"item:{item.Id}";

  private static NewsFeedItem[] UpdateVote(IReadOnlyList<NewsFeedItem> source, NewsFeedItem votedItem, ElectionVoteChoice? choice) =>
      source.Select(item => MatchesVoteTarget(item, votedItem) ? ApplyVote(item, choice) : item).ToArray();

  private static bool MatchesVoteTarget(NewsFeedItem item, NewsFeedItem votedItem) =>
      votedItem.Kind == NewsFeedItemKind.Source && votedItem.TopicId is { } topicId
          ? item.Kind == NewsFeedItemKind.Source && item.TopicId == topicId
          : item.Id == votedItem.Id;

  private Task SubmitVoteAsync(NewsFeedItem item, ElectionVoteChoice? choice, CancellationToken cancellationToken) =>
      item.Kind == NewsFeedItemKind.Source && item.TopicId is { } topicId
          ? choice is { } topicChoice ? newsFeedService.VoteTopicAsync(topicId, topicChoice, cancellationToken) : newsFeedService.ClearTopicVoteAsync(topicId, cancellationToken)
          : choice is { } itemChoice ? newsFeedService.VoteRssFeedItemAsync(item.Id, itemChoice, cancellationToken) : newsFeedService.ClearRssFeedItemVoteAsync(item.Id, cancellationToken);

  private static NewsFeedItem ApplyVote(NewsFeedItem item, ElectionVoteChoice? choice)
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
      NewsFeedItem votedItem,
      NewsFeedItem[] previousTargetItems,
      string? errorMessage)
  {
    if (loadRequestId != mutationLoadRequestId || previousTargetItems.Length == 0)
    {
      return;
    }

    var previousByRow = previousTargetItems.ToDictionary(item => item.FeedRowId, StringComparer.Ordinal);
    Items = Items.Select(item =>
        MatchesVoteTarget(item, votedItem) && previousByRow.TryGetValue(item.FeedRowId, out var previous)
            ? previous
            : item).ToArray();
    ErrorMessage = errorMessage;
  }
}

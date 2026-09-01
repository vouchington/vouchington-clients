using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Voting;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostsListViewModel
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native vote mutations surface API failures in view state before MAUI async event handlers observe them.")]
  public async Task VotePostAsync(
      PostRow item,
      ElectionVoteChoice? choice,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (!item.HasVoteCounts) return;
    if (!TryBeginVote(item.Id)) return;

    var previousItems = Items;
    var previousItem = previousItems.FirstOrDefault(row => row.Id == item.Id);
    var mutationLoadRequestId = requestId;
    var optimisticItems = UpdateVote(previousItems, item.Id, choice);
    Items = optimisticItems;
    ErrorMessage = null;
    try
    {
      await EmailVerificationGate.RunAsync(
          () => choice is { } selected
              ? postsService.VotePostAsync(item.Id, selected, cancellationToken)
              : postsService.ClearPostVoteAsync(item.Id, cancellationToken),
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
      FinishVote(item.Id);
    }
  }

  private PostRow RowFrom(
      Post post,
      IReadOnlyDictionary<string, PostElection>? elections,
      IReadOnlyDictionary<string, ElectionVote>? votes,
      IReadOnlyDictionary<string, BookmarkPredicates>? bookmarks,
      IReadOnlyDictionary<string, string>? markdownToHtml = null,
      IReadOnlyDictionary<string, UrlEmbed>? embeds = null) =>
      PostRows.From(post, elections, votes, bookmarks, markdownToHtml, embeds, localization);

  private static PostRow[] UpdateVote(IReadOnlyList<PostRow> source, string postId, ElectionVoteChoice? choice) =>
      source.Select(item => item.Id == postId ? ApplyVote(item, choice) : item).ToArray();

  private static PostRow ApplyVote(PostRow item, ElectionVoteChoice? choice)
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

  private bool TryBeginVote(string postId) =>
      votingIds.Add(postId);

  private void FinishVote(string postId) =>
      votingIds.Remove(postId);

  private void RollbackVote(
      int mutationLoadRequestId,
      string postId,
      PostRow? previousItem,
      string? errorMessage)
  {
    if (requestId != mutationLoadRequestId || previousItem is null)
    {
      return;
    }

    Items = Items.Select(item => item.Id == postId ? previousItem : item).ToArray();
    ErrorMessage = errorMessage;
  }
}

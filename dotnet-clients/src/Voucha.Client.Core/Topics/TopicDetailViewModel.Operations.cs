using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Voting;

namespace Voucha.Client.Core.Topics;

public sealed partial class TopicDetailViewModel
{
  public Task LoadAsync(string topicIdOrSlug, CancellationToken cancellationToken = default) =>
      LoadAsync(topicIdOrSlug, followSource: true, cancellationToken);

  public async Task LoadAsync(
      string topicIdOrSlug,
      bool followSource,
      CancellationToken cancellationToken = default)
  {
    State = LoadState.Loading;
    ErrorMessage = null;
    try
    {
      var response = await topicsService.FetchTopicAsync(topicIdOrSlug, cancellationToken).ConfigureAwait(true);
      ApplyTopicResponse(response);
      if (followSource && response.Topic.TopicType == "rss_feed")
      {
        await LoadSourceAsync(response.Topic.Id, cancellationToken).ConfigureAwait(true);
      }
      State = LoadState.Loaded;
    }
    catch (Exception ex) when (ex is OperationCanceledException or VouchaApiException or HttpRequestException)
    {
      State = ex is OperationCanceledException ? LoadState.Idle : LoadState.Error;
      ErrorMessage = ex is OperationCanceledException ? null : ex.Message;
      Topic = null;
    }
  }

  public async Task ToggleTopicFollowAsync(CancellationToken cancellationToken = default)
  {
    if (Topic is null) return;
    try
    {
      ErrorMessage = null;
      if (IsFollowingTopic)
      {
        await topicsService.UnfollowTopicAsync(Topic.Id, cancellationToken).ConfigureAwait(true);
        IsFollowingTopic = false;
        return;
      }
      await topicsService.FollowTopicAsync(Topic.Id, cancellationToken).ConfigureAwait(true);
      IsFollowingTopic = true;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      ErrorMessage = ex.Message;
    }
  }

  public async Task ToggleSourceFollowAsync(CancellationToken cancellationToken = default)
  {
    if (!CanFollowSource || SourceRssFeedId is null) return;
    try
    {
      ErrorMessage = null;
      if (IsFollowingSource)
      {
        await topicsService.UnfollowSourceAsync(SourceRssFeedId, cancellationToken).ConfigureAwait(true);
        IsFollowingSource = false;
        return;
      }
      await topicsService.FollowSourceAsync(SourceRssFeedId, cancellationToken).ConfigureAwait(true);
      IsFollowingSource = true;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      ErrorMessage = ex.Message;
    }
  }

  public async Task VoteTopicAsync(ElectionVoteChoice? choice, CancellationToken cancellationToken = default)
  {
    if (Topic is null) return;
    var previousVote = (VoteScoreNet, VoteCountUp, VoteCountDown, CurrentVoteChoice);
    try
    {
      ErrorMessage = null;
      ApplyVote(VoteCalculator.Apply(CurrentVoteChoice, VoteScoreNet, VoteCountUp, VoteCountDown, choice));
      await EmailVerificationGate.RunAsync(
          () => choice is { } selected ? topicsService.VoteTopicAsync(Topic.Id, selected, cancellationToken) : topicsService.ClearTopicVoteAsync(Topic.Id, cancellationToken),
          ex =>
          {
            ApplyVote(previousVote);
            ErrorMessage = ex.Message;
          }).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      ApplyVote(previousVote);
      throw;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      ApplyVote(previousVote);
      ErrorMessage = ex.Message;
    }
  }

  private void ApplyTopicResponse(TopicResponse response)
  {
    Topic = response.Topic;
    TopicElection = response.TopicElection;
    ElectionVote = response.ElectionVote;
    IsFollowingTopic = BookmarkSidecar.IsActive(response.Bookmarks, response.Topic.Id, BookmarkPredicate.Follow);
    IsMutedTopic = BookmarkSidecar.IsActive(response.Bookmarks, response.Topic.Id, BookmarkPredicate.Mute);
    ApplyVote((response.TopicElection?.VotesScoreNet, response.TopicElection?.VotesCountUp, response.TopicElection?.VotesCountDown, response.ElectionVote?.Choice));
  }

  private void ApplyVote((double? Net, int? Up, int? Down, ElectionVoteChoice? Current) vote)
  {
    VoteScoreNet = vote.Net;
    VoteCountUp = vote.Up;
    VoteCountDown = vote.Down;
    CurrentVoteChoice = vote.Current;
  }

  private async Task LoadSourceAsync(string topicId, CancellationToken cancellationToken)
  {
    var feeds = await topicsService.FetchRssFeedsForTopicAsync(topicId, cancellationToken: cancellationToken).ConfigureAwait(true);
    var feed = feeds.Results.Count > 0 ? feeds.Results[0] : null;
    SourceRssFeedId = feed?.Id;
    IsFollowingSource = feed is not null &&
        BookmarkSidecar.IsActive(feeds.Bookmarks, feed.Id, BookmarkPredicate.Follow);
    IsMutedSource = feed is not null &&
        BookmarkSidecar.IsActive(feeds.Bookmarks, feed.Id, BookmarkPredicate.Mute);
  }
}

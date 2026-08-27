using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;

namespace Voucha.Client.Core.Topics;

public sealed partial class TopicDetailViewModel
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native topic mutations surface API failures in view state before MAUI async event handlers observe them.")]
  public async Task ToggleTopicMuteAsync(CancellationToken cancellationToken = default)
  {
    if (Topic is null) return;
    var muted = !IsMutedTopic;
    var previousState = (IsMutedTopic, IsFollowingTopic);
    ErrorMessage = null;
    IsMutedTopic = muted;
    if (muted)
    {
      IsFollowingTopic = false;
    }

    try
    {
      await bookmarkService.SetAsync("topic", Topic.Id, BookmarkPredicate.Mute, muted, cancellationToken)
          .ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      RestoreTopicBookmarkState(previousState);
    }
    catch (Exception ex)
    {
      RestoreTopicBookmarkState(previousState);
      ErrorMessage = ex.Message;
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native topic mutations surface API failures in view state before MAUI async event handlers observe them.")]
  public async Task ToggleSourceMuteAsync(CancellationToken cancellationToken = default)
  {
    if (!CanMuteSource || SourceRssFeedId is null) return;
    var muted = !IsMutedSource;
    var previousState = (IsMutedSource, IsFollowingSource);
    ErrorMessage = null;
    IsMutedSource = muted;
    if (muted)
    {
      IsFollowingSource = false;
    }

    try
    {
      await bookmarkService.SetAsync("rss_feed", SourceRssFeedId, BookmarkPredicate.Mute, muted, cancellationToken)
          .ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      RestoreSourceBookmarkState(previousState);
    }
    catch (Exception ex)
    {
      RestoreSourceBookmarkState(previousState);
      ErrorMessage = ex.Message;
    }
  }

  private void RestoreTopicBookmarkState((bool Muted, bool Following) previousState)
  {
    IsMutedTopic = previousState.Muted;
    IsFollowingTopic = previousState.Following;
  }

  private void RestoreSourceBookmarkState((bool Muted, bool Following) previousState)
  {
    IsMutedSource = previousState.Muted;
    IsFollowingSource = previousState.Following;
  }
}

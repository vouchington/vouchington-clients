using System.Net;
using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native story discussion mutations surface API failures in view state before MAUI async event handlers observe them.")]
  public async Task<StoryDiscussionResult?> StartStoryDiscussionAsync(
      NewsFeedItem item,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (!item.CanStartStoryDiscussion || item.StoryId is not { } storyId) return null;
    if (newsFeedService is not IStoryDiscussionService storyDiscussionService) return null;
    if (!startingStoryDiscussionStoryIds.Add(storyId)) return null;

    var mutationLoadRequestId = loadRequestId;
    Items = SetStoryDiscussionStarting(Items, storyId, true);
    ErrorMessage = null;
    try
    {
      var result = await EmailVerificationGate.RunAsync<StoryDiscussionResult?>(
          async () => await storyDiscussionService.CreateStoryDiscussionAsync(
              storyId,
              item.Id,
              cancellationToken).ConfigureAwait(true),
          ex =>
          {
            if (mutationLoadRequestId == loadRequestId)
            {
              Items = SetStoryDiscussionStarting(Items, storyId, false);
              ErrorMessage = ex.Message;
            }
            return null;
          }).ConfigureAwait(true);
      if (result is null) return null;
      if (mutationLoadRequestId == loadRequestId)
      {
        Items = SetStoryDiscussionPost(Items, storyId, result.PostId);
      }

      return result;
    }
    catch (OperationCanceledException)
    {
      if (mutationLoadRequestId == loadRequestId)
      {
        Items = SetStoryDiscussionStarting(Items, storyId, false);
      }

      return null;
    }
    catch (VouchaApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
    {
      if (mutationLoadRequestId == loadRequestId)
      {
        try
        {
          await LoadAsync(cancellationToken).ConfigureAwait(true);
          return FindStoryDiscussionResult(storyId);
        }
        catch (OperationCanceledException)
        {
          Items = SetStoryDiscussionStarting(Items, storyId, false);
        }
      }

      return null;
    }
    catch (Exception ex)
    {
      if (mutationLoadRequestId == loadRequestId)
      {
        Items = SetStoryDiscussionStarting(Items, storyId, false);
        ErrorMessage = ex.Message;
      }

      return null;
    }
    finally
    {
      startingStoryDiscussionStoryIds.Remove(storyId);
    }
  }

  private static NewsFeedItem[] SetStoryDiscussionStarting(
      IReadOnlyList<NewsFeedItem> source,
      string storyId,
      bool isStarting) =>
      source.Select(item => string.Equals(item.StoryId, storyId, StringComparison.Ordinal)
          ? item with { IsStartingStoryDiscussion = isStarting }
          : item).ToArray();

  private static NewsFeedItem[] SetStoryDiscussionPost(
      IReadOnlyList<NewsFeedItem> source,
      string storyId,
      string postId) =>
      source.Select(item => string.Equals(item.StoryId, storyId, StringComparison.Ordinal)
          ? item with { StoryPostId = postId, IsStartingStoryDiscussion = false }
          : item).ToArray();

  private StoryDiscussionResult? FindStoryDiscussionResult(string? storyId)
  {
    if (string.IsNullOrWhiteSpace(storyId))
    {
      return null;
    }

    var postId = Items.FirstOrDefault(item =>
            string.Equals(item.StoryId, storyId, StringComparison.Ordinal))?.StoryPostId;
    return string.IsNullOrWhiteSpace(postId) ? null : new StoryDiscussionResult(postId);
  }
}

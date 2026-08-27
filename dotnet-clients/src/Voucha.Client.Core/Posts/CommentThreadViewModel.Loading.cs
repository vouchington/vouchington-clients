using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Posts;

public sealed partial class CommentThreadViewModel
{
  private async Task LoadInternalAsync(string? focusedCommentId, CancellationToken cancellationToken)
  {
    var normalizedFocusedCommentId = NormalizeFocusedCommentId(focusedCommentId);
    ResetForLoad(normalizedFocusedCommentId);

    try
    {
      var rootTask = postsService.FetchPostAsync(rootPostId, cancellationToken);
      var descendantsTask = postsService.FetchPostDescendantsPageAsync(rootPostId, null, 100, cancellationToken);
      Task<PostThreadResponse>? ancestorsTask = normalizedFocusedCommentId is null
          ? null
          : postsService.FetchPostAncestorsAsync(normalizedFocusedCommentId, cancellationToken);

      if (ancestorsTask is null)
      {
        await Task.WhenAll(rootTask, descendantsTask).ConfigureAwait(true);
      }
      else
      {
        await Task.WhenAll(rootTask, descendantsTask, ancestorsTask).ConfigureAwait(true);
      }

      rootPostResponse = await rootTask.ConfigureAwait(true);
      ReplaceDescendantsEnvelope(await descendantsTask.ConfigureAwait(true));
      ancestorsResponse = ancestorsTask is null ? null : await ancestorsTask.ConfigureAwait(true);
      AncestorPosts = BuildAncestorPosts();
      FocusedComment = ResolveFocusedComment();
      RebuildComments();
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      State = LoadState.Error;
      ErrorMessage = ex.Message;
      Comments = [];
      AncestorPosts = [];
      FocusedComment = null;
    }
    finally
    {
      IsLoadingAncestors = false;
      IsLoadingDescendants = false;
    }
  }

  private void ResetForLoad(string? focusedCommentId)
  {
    State = LoadState.Loading;
    ErrorMessage = null;
    FocusedCommentId = NormalizeFocusedCommentId(focusedCommentId);
    rootPostResponse = null;
    descendantsResponse = null;
    descendantPages.Reset();
    NotifyDescendantPagination();
    ancestorsResponse = null;
    AncestorPosts = [];
    Comments = [];
    FocusedComment = null;
    IsLoadingDescendants = true;
    IsLoadingAncestors = focusedCommentId is not null;
  }

  private Post[] BuildAncestorPosts()
  {
    if (ancestorsResponse is null) return [];

    return ancestorsResponse.Results
        .Select(result => EntityId(result) is { } id && ancestorsResponse.Posts.TryGetValue(id, out var post) ? post : null)
        .Where(post => post is not null && !string.Equals(post!.Id, FocusedCommentId, StringComparison.Ordinal))
        .Select(post => post!)
        .ToArray();
  }

  private Post? ResolveFocusedComment()
  {
    if (FocusedCommentId is null) return null;

    if (ancestorsResponse?.Posts.TryGetValue(FocusedCommentId, out var ancestorFocused) == true)
    {
      return ancestorFocused;
    }

    return descendantsResponse?.Posts.TryGetValue(FocusedCommentId, out var descendantFocused) == true
        ? descendantFocused
        : null;
  }

  private void RebuildComments()
  {
    Comments = descendantsResponse is null ? [] : BuildThreadNodes(descendantsResponse, Sort);
  }
}

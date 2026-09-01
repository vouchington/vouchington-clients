using Voucha.Client.Core.Api;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Posts;

public sealed partial class CommentThreadViewModel
{
  private readonly CursorPaginationState<EntityReference, string> descendantPages =
      new(reference => EntityId(reference) ?? throw new InvalidOperationException("Comment reference requires an id."));

  public bool HasMoreDescendants => descendantPages.HasLoadedPage && descendantPages.HasMore;
  public bool IsLoadingMoreDescendants => descendantPages.IsLoading;
  public bool HasDescendantPaginationError => descendantPages.LastError is not null;

  public async Task LoadMoreDescendantsAsync(CancellationToken cancellationToken = default)
  {
    var request = descendantPages.BeginNextPage();
    if (request is null) return;
    NotifyDescendantPagination();
    try
    {
      var response = await postsService.FetchPostDescendantsPageAsync(
          rootPostId,
          request.Cursor,
          100,
          cancellationToken).ConfigureAwait(true);
      if (!descendantPages.Complete(
          request,
          response.Results,
          response.PageInfo.EndCursor,
          response.PageInfo.HasNextPage || response.PageInfo.HasMore == true)) return;
      descendantsResponse = MergeDescendantsEnvelope(
          descendantsResponse ?? response,
          response,
          descendantPages.Items);
      FocusedComment = ResolveFocusedComment();
      RebuildComments();
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      descendantPages.Cancel(request);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      descendantPages.Fail(request, ex.Message);
    }
    finally
    {
      NotifyDescendantPagination();
    }
  }

  private void ReplaceDescendantsEnvelope(PostThreadResponse response)
  {
    descendantPages.Reset(response.Results);
    descendantPages.RestoreContinuation(
        response.PageInfo.EndCursor,
        response.PageInfo.HasNextPage || response.PageInfo.HasMore == true);
    descendantsResponse = descendantPages.Items.Count == response.Results.Count
        ? response
        : response with { Results = descendantPages.Items };
    NotifyDescendantPagination();
  }

  private static PostThreadResponse MergeDescendantsEnvelope(
      PostThreadResponse previous,
      PostThreadResponse next,
      IReadOnlyList<EntityReference> results) =>
      next with
      {
        Results = results,
        Posts = Merge(previous.Posts, next.Posts),
        Users = MergeNullable(previous.Users, next.Users),
        Communities = MergeNullable(previous.Communities, next.Communities),
        PostsMetrics = MergeNullable(previous.PostsMetrics, next.PostsMetrics),
        PostElections = MergeNullable(previous.PostElections, next.PostElections),
        ElectionVotes = MergeNullable(previous.ElectionVotes, next.ElectionVotes),
        MarkdownToHtml = MergeNullable(previous.MarkdownToHtml, next.MarkdownToHtml),
        Bookmarks = MergeNullable(previous.Bookmarks, next.Bookmarks),
        PostLinkEmbeds = MergeNullable(previous.PostLinkEmbeds, next.PostLinkEmbeds),
      };

  private static Dictionary<string, T> Merge<T>(
      IReadOnlyDictionary<string, T> previous,
      IReadOnlyDictionary<string, T> next) =>
      previous.Concat(next).GroupBy(pair => pair.Key, StringComparer.Ordinal)
          .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);

  private static IReadOnlyDictionary<string, T>? MergeNullable<T>(
      IReadOnlyDictionary<string, T>? previous,
      IReadOnlyDictionary<string, T>? next) =>
      previous is null ? next : next is null ? previous : Merge(previous, next);

  private bool TombstoneDescendant(string postId)
  {
    if (descendantsResponse is null || !descendantsResponse.Posts.ContainsKey(postId)) return false;
    descendantPages.Remove(reference => EntityId(reference) == postId);
    descendantsResponse = descendantsResponse with
    {
      Results = descendantPages.Items,
      Posts = WithoutKey(descendantsResponse.Posts, postId),
      PostsMetrics = WithoutNullableKey(descendantsResponse.PostsMetrics, postId),
      PostElections = WithoutNullableKey(descendantsResponse.PostElections, postId),
      ElectionVotes = WithoutNullableKey(descendantsResponse.ElectionVotes, postId),
      MarkdownToHtml = WithoutNullableKey(descendantsResponse.MarkdownToHtml, postId),
      Bookmarks = WithoutNullableKey(descendantsResponse.Bookmarks, postId),
      PostLinkEmbeds = WithoutNullableKey(descendantsResponse.PostLinkEmbeds, postId),
    };
    RebuildComments();
    return true;
  }

  private static Dictionary<string, T> WithoutKey<T>(
      IReadOnlyDictionary<string, T> source,
      string key) =>
      source.Where(pair => pair.Key != key).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

  private static Dictionary<string, T>? WithoutNullableKey<T>(
      IReadOnlyDictionary<string, T>? source,
      string key) =>
      source is null ? null : WithoutKey(source, key);

  private void NotifyDescendantPagination()
  {
    OnPropertyChanged(nameof(HasMoreDescendants));
    OnPropertyChanged(nameof(IsLoadingMoreDescendants));
    OnPropertyChanged(nameof(HasDescendantPaginationError));
  }
}

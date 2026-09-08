using Voucha.Client.Core.Api;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Posts;

public sealed partial class CommentThreadViewModel
{
  private const int AncestorPageSize = 5;
  private readonly CursorPaginationState<EntityReference, string> ancestorPages =
      new(reference => EntityId(reference) ?? throw new InvalidOperationException("Comment reference requires an id."));

  public bool HasMoreAncestors => ancestorPages.HasLoadedPage && ancestorPages.HasMore;
  public bool IsLoadingMoreAncestors => ancestorPages.IsLoading;
  public bool HasAncestorPaginationError => ancestorPages.LastError is not null;

  public async Task LoadMoreAncestorsAsync(CancellationToken cancellationToken = default)
  {
    if (FocusedCommentId is null) return;
    var request = ancestorPages.BeginNextPage();
    if (request is null) return;
    NotifyAncestorPagination();
    try
    {
      var response = await postsService.FetchPostAncestorsPageAsync(
          FocusedCommentId,
          request.Cursor,
          AncestorPageSize,
          cancellationToken).ConfigureAwait(true);
      if (!ancestorPages.CompletePrepending(
          request,
          response.Results,
          response.PageInfo.EndCursor,
          response.PageInfo.HasNextPage || response.PageInfo.HasMore == true)) return;
      ancestorsResponse = MergeAncestorEnvelope(
          ancestorsResponse ?? response,
          response,
          ancestorPages.Items);
      AncestorPosts = BuildAncestorPosts();
      FocusedComment = ResolveFocusedComment();
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      ancestorPages.Cancel(request);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ancestorPages.Fail(request, ex.Message);
    }
    finally
    {
      NotifyAncestorPagination();
    }
  }

  private void ReplaceAncestorsEnvelope(PostThreadResponse response)
  {
    ancestorPages.Reset(response.Results);
    ancestorPages.RestoreContinuation(
        response.PageInfo.EndCursor,
        response.PageInfo.HasNextPage || response.PageInfo.HasMore == true);
    ancestorsResponse = ancestorPages.Items.Count == response.Results.Count
        ? response
        : response with { Results = ancestorPages.Items };
    NotifyAncestorPagination();
  }

  private static PostThreadResponse MergeAncestorEnvelope(
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

  private void NotifyAncestorPagination()
  {
    OnPropertyChanged(nameof(HasMoreAncestors));
    OnPropertyChanged(nameof(IsLoadingMoreAncestors));
    OnPropertyChanged(nameof(HasAncestorPaginationError));
  }
}

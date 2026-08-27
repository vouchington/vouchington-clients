using Voucha.Client.Core.Api;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Tags;

public sealed partial class TagManagementViewModel
{
  private readonly CursorPaginationState<TagRelationRow, string> relationPages = new(row => row.Id);

  public bool HasMoreRelations => relationPages.HasLoadedPage && relationPages.HasMore;
  public bool IsLoadingMoreRelations => IsLoading || relationPages.IsLoading;
  public bool HasRelationPaginationError => relationPages.LastError is not null;

  public async Task LoadMoreRelationsAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading || Context is null || SelectedTab is null) return;
    var context = Context;
    var tab = SelectedTab;
    var request = relationPages.BeginNextPage();
    if (request is null) return;
    ErrorMessage = null;
    NotifyRelationPagination();
    try
    {
      var response = await client.FetchEntityRelationsAsync(
          new EntityRelationsRequest(
              context.EntityType,
              context.EntityIdOrSlug,
              tab.Predicate,
              tab.ObjectType,
              PositiveNetVoteScore: false,
              Limit: 25,
              After: request.Cursor),
          cancellationToken).ConfigureAwait(true);
      if (Context != context || SelectedTab != tab) return;
      if (relationPages.Complete(
          request,
          MapRelations(response),
          response.PageInfo.EndCursor,
          response.PageInfo.HasNextPage || response.PageInfo.HasMore == true))
      {
        Relations = relationPages.Items;
        ErrorMessage = null;
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      relationPages.Cancel(request);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      relationPages.Fail(request, ex.Message);
      ErrorMessage = ex.Message;
    }
    finally
    {
      NotifyRelationPagination();
    }
  }

  private void ReplaceRelationPage(EntityRelationsResponse response)
  {
    relationPages.Reset(MapRelations(response));
    relationPages.RestoreContinuation(
        response.PageInfo.EndCursor,
        response.PageInfo.HasNextPage || response.PageInfo.HasMore == true);
    Relations = relationPages.Items;
    NotifyRelationPagination();
  }

  private static TagRelationRow[] MapRelations(EntityRelationsResponse response) =>
      response.Results
          .Select(reference => reference.Id)
          .Where(id => response.EntityRelations.ContainsKey(id))
          .Select(id =>
          {
            var relation = response.EntityRelations[id];
            var myVote = response.ElectionVotes?.GetValueOrDefault(relation.Id)?.Choice;
            return new TagRelationRow(
                relation.Id,
                GetRelationTitle(relation),
                relation.ObjectId,
                relation.VotesScoreNet,
                myVote);
          })
          .ToArray();

  private void ResetRelationPagination()
  {
    relationPages.Reset();
    Relations = [];
    NotifyRelationPagination();
  }

  private void NotifyRelationPagination()
  {
    OnPropertyChanged(nameof(HasMoreRelations));
    OnPropertyChanged(nameof(IsLoadingMoreRelations));
    OnPropertyChanged(nameof(HasRelationPaginationError));
  }
}

using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private readonly CursorPaginationState<ProfileUserTagRow, string> userTagPages = new(row => row.Id);

  public bool HasMoreUserTags => userTagPages.HasLoadedPage && userTagPages.HasMore;
  public bool IsLoadingMoreUserTags => userTagPages.IsLoading;
  public bool HasUserTagPaginationError => userTagPages.LastError is not null;

  public async Task LoadMoreUserTagsAsync(CancellationToken cancellationToken = default)
  {
    if (!CanViewUserTags || User?.Id is not { Length: > 0 } userId || userTagClient is null) return;
    var loadVersion = userTagLoadVersion;
    var request = userTagPages.BeginNextPage();
    if (request is null) return;
    NotifyUserTagPagination();
    try
    {
      var response = await userTagClient.FetchEntityRelationsAsync(
          new EntityRelationsRequest(
              "user",
              userId,
              "category",
              "topic",
              PositiveNetVoteScore: true,
              Limit: 25,
              After: request.Cursor),
          cancellationToken).ConfigureAwait(true);
      if (loadVersion != userTagLoadVersion || User?.Id != userId) return;
      if (userTagPages.Complete(
          request,
          MapUserTags(response),
          response.PageInfo.EndCursor,
          response.PageInfo.HasNextPage || response.PageInfo.HasMore == true)) UserTags = userTagPages.Items;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      userTagPages.Cancel(request);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      userTagPages.Fail(request, ex.Message);
    }
    finally
    {
      NotifyUserTagPagination();
    }
  }

  private void ReplaceUserTagPage(EntityRelationsResponse response)
  {
    userTagPages.Reset(MapUserTags(response));
    userTagPages.RestoreContinuation(
        response.PageInfo.EndCursor,
        response.PageInfo.HasNextPage || response.PageInfo.HasMore == true);
    UserTags = userTagPages.Items;
    NotifyUserTagPagination();
  }

  private static ProfileUserTagRow[] MapUserTags(EntityRelationsResponse response) =>
      response.Results
          .Select(reference => reference.Id)
          .Where(id => response.EntityRelations.ContainsKey(id))
          .Select(id =>
          {
            var relation = response.EntityRelations[id];
            var title = relation.ObjectData is JsonElement data
                ? UserTagTitleKeys
                    .Select(key => data.TryGetProperty(key, out var value) &&
                        value.ValueKind == JsonValueKind.String ? value.GetString() : null)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? relation.Id
                : relation.Id;
            var vote = response.ElectionVotes?.GetValueOrDefault(relation.Id)?.Choice;
            return new ProfileUserTagRow(
                relation.Id,
                title,
                relation.VotesCountUp ?? 0,
                relation.VotesCountDown ?? 0,
                vote);
          })
          .ToArray();

  private void NotifyUserTagPagination()
  {
    OnPropertyChanged(nameof(HasMoreUserTags));
    OnPropertyChanged(nameof(IsLoadingMoreUserTags));
    OnPropertyChanged(nameof(HasUserTagPaginationError));
  }
}

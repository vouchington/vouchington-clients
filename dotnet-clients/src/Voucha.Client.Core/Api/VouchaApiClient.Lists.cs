namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<ListsSearchResponse> FetchListsAsync(
      FetchListsRequest? request = null,
      CancellationToken cancellationToken = default)
  {
    var fetchRequest = request ?? new FetchListsRequest();
    return SendAsync<ListsSearchResponse>(
          VouchaApiEndpoints.Lists(fetchRequest.After, fetchRequest.Limit),
          cancellationToken);
  }

  public Task<ListResponse> CreateListAsync(
      ListMutationBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<ListResponse>(
          VouchaApiEndpoints.CreateList(Require(body)),
          cancellationToken);

  public Task<ListResponse> FetchListAsync(
      string listId,
      CancellationToken cancellationToken = default) =>
      SendAsync<ListResponse>(
          VouchaApiEndpoints.List(listId),
          cancellationToken);

  public Task<ListResponse> UpdateListAsync(
      string listId,
      ListMutationBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<ListResponse>(
          VouchaApiEndpoints.UpdateList(listId, Require(body)),
          cancellationToken);

  public Task DeleteListAsync(
      string listId,
      CancellationToken cancellationToken = default) =>
      SendAsync(
          VouchaApiEndpoints.DeleteList(listId),
          cancellationToken);

  public Task<ListItemsResponse> FetchListItemsAsync(
      FetchListItemsRequest request,
      CancellationToken cancellationToken = default)
  {
    var requiredRequest = Require(request);
    return SendAsync<ListItemsResponse>(
          VouchaApiEndpoints.ListItems(
              requiredRequest.ListId,
              requiredRequest.MediaType,
              requiredRequest.Read,
              requiredRequest.After,
              requiredRequest.Limit),
          cancellationToken);
  }

  public Task<ListItemResponse> AddListRssFeedItemAsync(
      string listId,
      string rssFeedItemId,
      CancellationToken cancellationToken = default) =>
      SendAsync<ListItemResponse>(
          VouchaApiEndpoints.AddListRssFeedItem(listId, new AddListRssFeedItemBody(rssFeedItemId)),
          cancellationToken);

  public Task RemoveListRssFeedItemAsync(
      string listId,
      string rssFeedItemId,
      CancellationToken cancellationToken = default) =>
      SendAsync(
          VouchaApiEndpoints.RemoveListRssFeedItem(listId, rssFeedItemId),
          cancellationToken);

  public Task<ListItemResponse> AddListPostAsync(
      string listId,
      string postId,
      CancellationToken cancellationToken = default) =>
      SendAsync<ListItemResponse>(
          VouchaApiEndpoints.AddListPost(listId, new AddListPostBody(postId)),
          cancellationToken);

  public Task RemoveListPostAsync(
      string listId,
      string postId,
      CancellationToken cancellationToken = default) =>
      SendAsync(
          VouchaApiEndpoints.RemoveListPost(listId, postId),
          cancellationToken);

  public Task<ListsContainingResponse> FetchListsContainingAsync(
      string itemType,
      string entityId,
      CancellationToken cancellationToken = default) =>
      SendAsync<ListsContainingResponse>(
          VouchaApiEndpoints.ListsContaining(itemType, entityId),
          cancellationToken);

  public Task<ImportCommunityListResponse> ImportCommunityListAsync(
      string listId,
      string communitySlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<ImportCommunityListResponse>(
          VouchaApiEndpoints.ImportCommunityList(listId, new ImportCommunityListBody(communitySlug)),
          cancellationToken);

  public Task MarkRssFeedItemReadAsync(
      string rssFeedItemId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.MarkRssFeedItemRead(rssFeedItemId), cancellationToken);

  public Task MarkRssFeedItemUnreadAsync(
      string rssFeedItemId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.MarkRssFeedItemUnread(rssFeedItemId), cancellationToken);

  public Task MarkPostReadAsync(
      string postId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.MarkPostRead(postId), cancellationToken);

  public Task MarkPostUnreadAsync(
      string postId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.MarkPostUnread(postId), cancellationToken);
}

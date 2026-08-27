using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Bookmarks;

public sealed class ApiBookmarkService : IBookmarkService
{
  private readonly VouchaApiClient client;

  public ApiBookmarkService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task SetAsync(
      string entityType,
      string entityId,
      BookmarkPredicate predicate,
      bool active,
      CancellationToken cancellationToken = default) =>
      client.SendAsync(
          active
              ? VouchaApiEndpoints.Bookmark(entityType, entityId, predicate.ToApiName())
              : VouchaApiEndpoints.Unbookmark(entityType, entityId, predicate.ToApiName()),
          cancellationToken);
}

using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Bookmarks;

public sealed class NullBookmarkService : IBookmarkService
{
  public static readonly IBookmarkService Instance = new NullBookmarkService();

  private NullBookmarkService()
  {
  }

  public Task SetAsync(
      string entityType,
      string entityId,
      BookmarkPredicate predicate,
      bool active,
      CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
}

using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Bookmarks;

public interface IBookmarkService
{
  Task SetAsync(
      string entityType,
      string entityId,
      BookmarkPredicate predicate,
      bool active,
      CancellationToken cancellationToken = default);
}

using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Profiles;

public interface IProfileSafetyService
{
  Task<BookmarkPredicates> FetchUserBookmarksAsync(
      string userId,
      CancellationToken cancellationToken = default);

  Task SetUserBookmarkAsync(
      string userId,
      BookmarkPredicate predicate,
      bool active,
      CancellationToken cancellationToken = default);

  Task ReportUserAsync(
      string userId,
      string reason,
      string? note = null,
      string? turnstileToken = null,
      CancellationToken cancellationToken = default);
}

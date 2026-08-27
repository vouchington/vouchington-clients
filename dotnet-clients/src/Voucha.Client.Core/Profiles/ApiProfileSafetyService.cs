using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;

namespace Voucha.Client.Core.Profiles;

public sealed class ApiProfileSafetyService : IProfileSafetyService
{
  private readonly VouchaApiClient client;
  private readonly IBookmarkService bookmarkService;

  public ApiProfileSafetyService(VouchaApiClient client, IBookmarkService bookmarkService)
  {
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.bookmarkService = bookmarkService ?? throw new ArgumentNullException(nameof(bookmarkService));
  }

  public async Task<BookmarkPredicates> FetchUserBookmarksAsync(
      string userId,
      CancellationToken cancellationToken = default)
  {
    var response = await client.SendAsync<EntityBookmarksResponse>(
        VouchaApiEndpoints.EntityBookmarks("user", userId),
        cancellationToken).ConfigureAwait(false);
    return response?.Bookmarks ?? new();
  }

  public Task SetUserBookmarkAsync(
      string userId,
      BookmarkPredicate predicate,
      bool active,
      CancellationToken cancellationToken = default) =>
      bookmarkService.SetAsync("user", userId, predicate, active, cancellationToken);

  public Task ReportUserAsync(
      string userId,
      string reason,
      string? note = null,
      string? turnstileToken = null,
      CancellationToken cancellationToken = default) =>
      client.ReportAsync(new ReportBody("user", userId, reason, note, turnstileToken), cancellationToken);
}

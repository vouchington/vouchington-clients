using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Posts;

public partial interface ICommentThreadService
{
  Task<PostThreadResponse> FetchPostDescendantsPageAsync(
      string postIdOrSlug,
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      FetchPostDescendantsAsync(postIdOrSlug, cancellationToken);
}

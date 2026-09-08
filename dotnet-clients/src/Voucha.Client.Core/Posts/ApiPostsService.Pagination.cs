using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Posts;

public sealed partial class ApiPostsService
{
  public Task<PostThreadResponse> FetchPostDescendantsPageAsync(
      string postIdOrSlug,
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      client.FetchPostDescendantsPageAsync(postIdOrSlug, after, limit, cancellationToken);

  public Task<PostThreadResponse> FetchPostAncestorsPageAsync(
      string postIdOrSlug,
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      client.FetchPostAncestorsPageAsync(postIdOrSlug, after, limit, cancellationToken);
}

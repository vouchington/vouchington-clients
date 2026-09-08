using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Posts;

public sealed partial class CommentThreadViewModelTests
{
  private sealed partial class RecordingPostsService
  {
    public Queue<object> AncestorPageResponses { get; } = [];

    public List<string?> AncestorPageCursors { get; } = [];

    public List<int> AncestorPageLimits { get; } = [];

    public Task<PostThreadResponse> FetchPostAncestorsPageAsync(
        string postIdOrSlug,
        string? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
      AncestorPageCursors.Add(after);
      AncestorPageLimits.Add(limit);
      if (after is null)
      {
        AncestorsRequested?.TrySetResult(true);
        return AncestorsResponseFactory is null
            ? Task.FromResult(PermalinkAncestorsResponse ?? throw new InvalidOperationException("Missing ancestors response."))
            : AncestorsResponseFactory();
      }

      var response = AncestorPageResponses.Dequeue();
      return response is Exception exception
          ? Task.FromException<PostThreadResponse>(exception)
          : Task.FromResult((PostThreadResponse)response);
    }
  }
}

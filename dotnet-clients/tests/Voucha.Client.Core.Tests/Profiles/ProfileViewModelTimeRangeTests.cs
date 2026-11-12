using Voucha.Client.Core.Api;
using Voucha.Client.Core.Profiles;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelSafetyTests
{
  [Fact]
  public async Task ProfileRangeChangeReplacesRowsResetsCursorAndPreservesAuthorAndType()
  {
    var requests = new List<FetchPostsRequest>();
    var posts = new RecordingPostsService
    {
      FetchPostsAsyncOverride = (_, request) =>
      {
        requests.Add(request);
        var id = request.TimeRange is null ? "default" : request.After is null ? "year" : "more";
        var cursor = request.After is null ? "opaque-cursor" : null;
        return Task.FromResult(new PostsFeedResponse(
            [Reference(id)], new PageInfo(cursor, cursor is not null, null),
            new Dictionary<string, Post> { [id] = new(id, "review", id, "Body", request.Creator) },
            new Dictionary<string, User>(), new Dictionary<string, Community>()));
      },
    };
    using var model = NewViewModel(new User("user-2", "bob", "Hello"), posts: posts);
    await model.LoadPublicScopeAsync("bob", new(NativeUserProfileSection.Posts,
        NativeUserProfileCollectionKind.Reviews), TestContext.Current.CancellationToken);
    await model.SelectTimeRangeAsync("1y", TestContext.Current.CancellationToken);
    Assert.Equal(new[] { "year" }, model.HistoryItems.Select(row => row.Id));
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(new[] { "year", "more" }, model.HistoryItems.Select(row => row.Id));
    await model.SelectTimeRangeAsync("1m", TestContext.Current.CancellationToken);
    Assert.Equal(new[] { "default" }, model.HistoryItems.Select(row => row.Id));
    Assert.Equal(new string?[] { null, "1y", "1y", null }, requests.Select(request => request.TimeRange));
    Assert.Equal(new string?[] { null, null, "opaque-cursor", null }, requests.Select(request => request.After));
    Assert.All(requests, request =>
    {
      Assert.Equal("user-2", request.Creator);
      Assert.Equal("review", request.PostTypes);
    });
  }
}

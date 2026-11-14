using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class HiddenCommentListTests
{
  [Fact]
  public async Task HiddenCommentRemainsInPostFeedAndHiddenPostsCollection()
  {
    const string Comment = """
        {"id":"hidden-comment","post_type":"comment","markdown":"Reply","parent_post_id":"parent","root_post_id":"root","created_by_id":"author"}
        """;
    var feedBody = $$$$"""
        {"results":[{"id":"hidden-comment"}],"posts":{"hidden-comment":{{{{Comment}}}}},
        "users":{},"communities":{},"page_info":{"has_next_page":false},"bookmarks":{"hidden-comment":{"hide":true}}}
        """;
    using var handler = new RecordingHandler([new RecordedResponse(feedBody)]);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    using var feed = new PostsListViewModel(new ApiPostsService(new VouchaApiClient(http)));
    await feed.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    var row = Assert.Single(feed.Items);
    Assert.Equal("hidden-comment", row.Id);
    Assert.True(row.IsHidden);
    Assert.Equal("Reply", row.Body);

    using var bookmarksHandler = new RecordingHandler([new RecordedResponse($$$$"""
        {"results":[{{{{Comment}}}}],"page_info":{"has_next_page":false}}
        """)]);
    using var bookmarksHttp = new HttpClient(bookmarksHandler) { BaseAddress = new Uri("https://api.test") };
    using var hidden = new BookmarkCollectionViewModel(new VouchaApiClient(bookmarksHttp), new Session());
    hidden.SetContext(new BookmarkCollectionRouteContext("/my/posts/hidden", UiText.Verbatim("Hidden"), BookmarkCollectionKind.Posts, "hidden"));
    await hidden.LoadAsync(TestContext.Current.CancellationToken);
    var hiddenRow = Assert.Single(hidden.Rows);
    Assert.Equal("hidden-comment", hiddenRow.Id);
    Assert.Equal(BookmarkPredicate.Hide, hiddenRow.InverseAction?.Predicate);
    Assert.Equal("/api/v1/users/viewer/posts/hidden?limit=25", bookmarksHandler.PathAndQuery);
  }

  private sealed class Session : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User("viewer", "viewer"));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

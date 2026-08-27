using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityDetailViewModelTests
{
  [Fact]
  public async Task LoadAsyncHydratesCommunityRowsFromSharedFixtures()
  {
    var (viewModel, handler) = CreateViewModel(
        ApiFixtureLoader.LoadResponse("web.communities.show.default"),
        ApiFixtureLoader.LoadResponse("web.communities.members.default"),
        ApiFixtureLoader.LoadResponse("web.communities.posts.default"),
        ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default"));

    await viewModel.LoadAsync("test-community", TestContext.Current.CancellationToken);

    Assert.Equal("Test Community", viewModel.Community?.Name);
    Assert.Equal("community-member-1", viewModel.Members[0].Id);
    Assert.Equal("Test User", viewModel.Members[0].DisplayName);
    Assert.Equal("post-1", viewModel.Posts[0].Id);
    Assert.Equal("Fixture post", viewModel.Posts[0].Title);
    Assert.Equal(1, viewModel.ListItemCounts?.Topic);
    Assert.False(viewModel.HasError);
    Assert.False(viewModel.IsLoading);
    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/communities/test-community", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/members?limit=20", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/posts?limit=20", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/list-items/counts", request.PathAndQuery));
  }

  [Fact]
  public async Task ArchiveAsyncAppliesReturnedArchiveState()
  {
    var (viewModel, handler) = CreateViewModel(
        SessionSnapshotForTests.Authenticated,
        CommunityDetailJsonWithMembership("web.communities.show.default", "owner"),
        ApiFixtureLoader.LoadResponse("web.communities.members.default"),
        ApiFixtureLoader.LoadResponse("web.communities.posts.default"),
        ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default"),
        ApiFixtureLoader.LoadResponse("web.communities.archive.default"));

    await viewModel.LoadAsync("test-community", TestContext.Current.CancellationToken);
    var archived = await viewModel.ArchiveAsync(TestContext.Current.CancellationToken);

    Assert.True(archived);
    Assert.True(viewModel.IsArchived);
    Assert.False(viewModel.CanArchive);
    Assert.True(viewModel.CanUnarchive);
    Assert.Equal(HttpMethod.Patch, handler.Requests[^1].Method);
    Assert.Equal("/api/v1/communities/test-community", handler.Requests[^1].PathAndQuery);
    Assert.Contains("\"archive\":true", handler.Requests[^1].Body!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task UnarchiveAsyncAppliesReturnedState()
  {
    var (viewModel, handler) = CreateViewModel(
        SessionSnapshotForTests.Authenticated,
        CommunityDetailJsonWithMembership("web.communities.archive.default", "owner"),
        ApiFixtureLoader.LoadResponse("web.communities.members.default"),
        ApiFixtureLoader.LoadResponse("web.communities.posts.default"),
        ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default"),
        ApiFixtureLoader.LoadResponse("web.communities.show.default"));

    await viewModel.LoadAsync("test-community", TestContext.Current.CancellationToken);
    var unarchived = await viewModel.UnarchiveAsync(TestContext.Current.CancellationToken);

    Assert.True(unarchived);
    Assert.False(viewModel.IsArchived);
    Assert.True(viewModel.CanArchive);
    Assert.False(viewModel.CanUnarchive);
    Assert.Equal(HttpMethod.Patch, handler.Requests[^1].Method);
    Assert.Contains("\"archive\":false", handler.Requests[^1].Body!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task JoinAsyncReloadsDetail()
  {
    var (viewModel, handler) = CreateViewModel(
        SessionSnapshotForTests.Authenticated,
        ApiFixtureLoader.LoadResponse("web.communities.show.default"),
        ApiFixtureLoader.LoadResponse("web.communities.members.default"),
        ApiFixtureLoader.LoadResponse("web.communities.posts.default"),
        ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default"),
        "{}",
        ApiFixtureLoader.LoadResponse("web.communities.show.default"),
        ApiFixtureLoader.LoadResponse("web.communities.members.default"),
        ApiFixtureLoader.LoadResponse("web.communities.posts.default"),
        ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default"));

    await viewModel.LoadAsync("test-community", TestContext.Current.CancellationToken);
    var changed = await viewModel.JoinAsync(TestContext.Current.CancellationToken);

    Assert.True(changed);
    Assert.False(viewModel.HasError);
    Assert.Equal(9, handler.Requests.Count);
    Assert.Equal(HttpMethod.Post, handler.Requests[4].Method);
    Assert.Equal("/api/v1/communities/test-community/members", handler.Requests[4].PathAndQuery);
  }

  [Fact]
  public async Task LeaveAsyncReloadsDetail()
  {
    var (viewModel, handler) = CreateViewModel(
        SessionSnapshotForTests.Authenticated,
        JoinedCommunityDetailJson(),
        ApiFixtureLoader.LoadResponse("web.communities.members.default"),
        ApiFixtureLoader.LoadResponse("web.communities.posts.default"),
        ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default"),
        "{}",
        ApiFixtureLoader.LoadResponse("web.communities.show.default"),
        ApiFixtureLoader.LoadResponse("web.communities.members.default"),
        ApiFixtureLoader.LoadResponse("web.communities.posts.default"),
        ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default"));

    await viewModel.LoadAsync("test-community", TestContext.Current.CancellationToken);
    var changed = await viewModel.LeaveAsync(TestContext.Current.CancellationToken);

    Assert.True(changed);
    Assert.False(viewModel.HasError);
    Assert.Equal(9, handler.Requests.Count);
    Assert.Equal(HttpMethod.Delete, handler.Requests[4].Method);
    Assert.Equal("/api/v1/communities/test-community/members", handler.Requests[4].PathAndQuery);
  }

  [Fact]
  public async Task ActionsReturnFalseWhenNotAllowed()
  {
    var viewModel = CreateViewModel().ViewModel;

    Assert.False(await viewModel.JoinAsync(TestContext.Current.CancellationToken));
    Assert.False(await viewModel.LeaveAsync(TestContext.Current.CancellationToken));
    Assert.False(await viewModel.ArchiveAsync(TestContext.Current.CancellationToken));
    Assert.False(await viewModel.UnarchiveAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task LoadAsyncCapturesApiErrors()
  {
    var handler = new RecordingHandler("{\"error\":\"nope\"}", System.Net.HttpStatusCode.BadRequest);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var service = new ApiCommunitiesService(client);
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadAsync("test-community", TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasError);
    Assert.False(viewModel.IsLoading);
    Assert.Equal("Voucha API request failed with HTTP 400.", viewModel.ErrorMessage);
  }

  private static (CommunityDetailViewModel ViewModel, RecordingHandler Handler) CreateViewModel(
      params string[] responses) =>
      CreateViewModel(null, responses);

  private static (CommunityDetailViewModel ViewModel, RecordingHandler Handler) CreateViewModel(
      SessionSnapshot? session,
      params string[] responses)
  {
    var handler = new RecordingHandler(responses.Select(response => new RecordedResponse(response)));
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var service = new ApiCommunitiesService(client);
    return (new CommunityDetailViewModel(service, new TestSessionStore(session)), handler);
  }

  private static string JoinedCommunityDetailJson()
  {
    return CommunityDetailJsonWithMembership("web.communities.show.default", "member");
  }

  private static string CommunityDetailJsonWithMembership(string fixtureId, string role)
  {
    var json = ApiFixtureLoader.LoadResponse(fixtureId).TrimEnd();
    return json[..^1] + $@",
  ""membership"": {{
    ""id"": ""community-member-1"",
    ""community_id"": ""community-1"",
    ""user_id"": ""user-1"",
    ""role"": ""{role}"",
    ""__entity_type"": ""community_member""
  }}
}}";
  }

  private sealed class TestSessionStore(SessionSnapshot? current) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current { get; } = current ?? SessionSnapshot.Anonymous;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private static class SessionSnapshotForTests
  {
    public static SessionSnapshot Authenticated { get; } = new(new User(
        Id: "user-1",
        Username: "testuser",
        Roles: ["user"],
        EmailAddress: "tests@example.com",
        MembershipPlan: "free"));
  }
}

using System.Text.Json.Nodes;
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

  [Theory]
  [InlineData("member", true, false, true, true)]
  [InlineData("member", false, false, true, false)]
  [InlineData("moderator", false, false, true, true)]
  [InlineData("owner", true, true, true, false)]
  [InlineData(null, true, false, true, false)]
  [InlineData("member", true, false, false, false)]
  public async Task InviteCreationFollowsMembershipSettingAndArchiveState(
      string? role, bool memberInvitesAllowed, bool archived, bool authenticated, bool expected)
  {
    var detail = JsonNode.Parse(CommunityDetailJsonWithMembership("web.communities.show.default", role ?? "member"))!;
    detail["community"]!["member_invites_allowed_at"] = memberInvitesAllowed ? "2026-07-01T00:00:00Z" : null;
    detail["community"]!["archived_at"] = archived ? "2026-07-01T00:00:00Z" : null;
    if (role is null) detail["membership"] = null;
    var (viewModel, _) = CreateViewModel(
        authenticated ? SessionSnapshotForTests.Authenticated : SessionSnapshot.Anonymous,
        detail.ToJsonString(),
        ApiFixtureLoader.LoadResponse("web.communities.members.default"),
        ApiFixtureLoader.LoadResponse("web.communities.posts.default"),
        ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default"));

    await viewModel.LoadAsync("test-community", TestContext.Current.CancellationToken);

    Assert.Equal(expected, viewModel.CanCreateInvite);
  }

  [Theory]
  [InlineData("member", false, true, false)]
  [InlineData("moderator", false, true, true)]
  [InlineData("owner", true, true, false)]
  [InlineData(null, false, true, false)]
  [InlineData("moderator", false, false, false)]
  public async Task InviteRevocationRequiresAuthenticatedUnarchivedCommunityModerator(
      string? role, bool archived, bool authenticated, bool expected)
  {
    var detail = JsonNode.Parse(CommunityDetailJsonWithMembership("web.communities.show.default", role ?? "member"))!;
    detail["community"]!["archived_at"] = archived ? "2026-07-01T00:00:00Z" : null;
    if (role is null) detail["membership"] = null;
    var (viewModel, _) = CreateViewModel(
        authenticated ? SessionSnapshotForTests.Authenticated : SessionSnapshot.Anonymous,
        detail.ToJsonString(),
        ApiFixtureLoader.LoadResponse("web.communities.members.default"),
        ApiFixtureLoader.LoadResponse("web.communities.posts.default"),
        ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default"));

    await viewModel.LoadAsync("test-community", TestContext.Current.CancellationToken);

    Assert.Equal(expected, viewModel.CanRevokeInvite);
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

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task PartialArchiveMutationRetainsCachedCreationFacts(bool archive)
  {
    var initial = JsonNode.Parse(ApiFixtureLoader.LoadResponse(
        archive ? "web.communities.show.default" : "web.communities.archive.default"))!;
    var imageDetail = JsonNode.Parse(ApiFixtureLoader.LoadResponse("web.communities.show.default"))!;
    initial["community"]!["profile_image_placement"] = imageDetail["community"]!["profile_image_placement"]!.DeepClone();
    initial["community"]!["banner_image_placement"] = imageDetail["community"]!["banner_image_placement"]!.DeepClone();
    initial["membership"] = JsonNode.Parse("""
        {"id":"community-member-1","community_id":"community-1","user_id":"user-1","role":"owner","__entity_type":"community_member"}
        """);
    initial["community"]!["provenance"] = JsonNode.Parse("""{"via":"mcp","app":null}""");
    initial["community"]!["staff_provenance"] = JsonNode.Parse("""{"created_via":"mcp","oauth_client":null}""");
    var partial = JsonNode.Parse(ApiFixtureLoader.LoadResponse(
        archive ? "web.communities.archive.default" : "web.communities.show.default"))!;
    partial["community"]!.AsObject().Remove("provenance");
    partial["community"]!.AsObject().Remove("staff_provenance");
    partial["community"]!.AsObject().Remove("profile_image_placement");
    partial["community"]!.AsObject().Remove("banner_image_placement");
    var (viewModel, _) = CreateViewModel(
        SessionSnapshotForTests.Authenticated,
        initial.ToJsonString(),
        ApiFixtureLoader.LoadResponse("web.communities.members.default"),
        ApiFixtureLoader.LoadResponse("web.communities.posts.default"),
        ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default"),
        partial.ToJsonString());

    await viewModel.LoadAsync("test-community", TestContext.Current.CancellationToken);
    Assert.NotNull(viewModel.Community?.Provenance);
    Assert.NotNull(viewModel.Community?.StaffProvenance);
    Assert.Equal("00000000-0000-7000-8000-000000000203", viewModel.Community?.ProfileImagePlacement?.ImageId);
    Assert.Equal("00000000-0000-7000-8000-000000000204", viewModel.Community?.BannerImagePlacement?.ImageId);

    Assert.True(archive
        ? await viewModel.ArchiveAsync(TestContext.Current.CancellationToken)
        : await viewModel.UnarchiveAsync(TestContext.Current.CancellationToken));
    Assert.Equal(archive, viewModel.IsArchived);
    Assert.NotNull(viewModel.Community?.Provenance);
    Assert.NotNull(viewModel.Community?.StaffProvenance);
    Assert.Equal("00000000-0000-7000-8000-000000000203", viewModel.Community?.ProfileImagePlacement?.ImageId);
    Assert.Equal("00000000-0000-7000-8000-000000000204", viewModel.Community?.BannerImagePlacement?.ImageId);
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

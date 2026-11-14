using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityApplicationDetailAuthorityTests
{
  [Fact]
  public async Task SuccessfulReadForAnotherApplicationShowsErrorWithoutPublishingPrivateContent()
  {
    var service = new ScriptedCommunitiesService { ApplicationDetail = Task.FromResult(Detail()) };
    using var detail = new CommunityApplicationDetailViewModel(service, "community", "another-application");

    await detail.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, detail.State);
    Assert.NotNull(detail.Error);
    Assert.Null(detail.Application);
  }

  [Theory]
  [InlineData("identity", false)]
  [InlineData("identity", true)]
  [InlineData("community", false)]
  [InlineData("community", true)]
  [InlineData("membership", false)]
  [InlineData("membership", true)]
  [InlineData("dispose", false)]
  [InlineData("dispose", true)]
  public async Task OwnerChangesClearLoadedDetailAndRejectLateResponses(string change, bool delayed)
  {
    var completion = new TaskCompletionSource<CommunityApplicationResponse>();
    var service = new ScriptedCommunitiesService { ApplicationDetail = delayed ? completion.Task : Task.FromResult(Detail()) };
    SeedLoadResponseSet(service, CreateDetailResponse(true, false, membershipRole: "moderator"));
    var session = new MutableSession();
    using var parent = new CommunityDetailViewModel(service, session);
    await parent.LoadAsync("community-1", TestContext.Current.CancellationToken);
    using var detail = parent.CreateApplicationDetail("application-1");
    var loading = detail.LoadAsync(TestContext.Current.CancellationToken);
    if (!delayed) Assert.NotNull(detail.Application);

    switch (change)
    {
      case "identity": session.ChangeIdentity(); break;
      case "dispose": parent.Dispose(); break;
      default:
        SeedLoadResponseSet(service, CreateDetailResponse(true, false, membershipRole: "member"));
        await parent.LoadAsync(change == "community" ? "another-community" : "community-1", TestContext.Current.CancellationToken);
        break;
    }
    if (delayed) completion.SetResult(Detail());
    await loading;

    Assert.Null(detail.Application);
    Assert.Null(detail.Error);
    Assert.Equal(LoadState.Idle, detail.State);
    await detail.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Null(detail.Application);
  }

  [Fact]
  public async Task ClosingTheDetailDetachesItsOwnerAndRejectsLaterReads()
  {
    var service = new ScriptedCommunitiesService { ApplicationDetail = Task.FromResult(Detail()) };
    SeedLoadResponseSet(service, CreateDetailResponse(true, false, membershipRole: "moderator"));
    using var parent = new CommunityDetailViewModel(service, new MutableSession());
    await parent.LoadAsync("community-1", TestContext.Current.CancellationToken);
    var detail = parent.CreateApplicationDetail("application-1");
    await detail.LoadAsync(TestContext.Current.CancellationToken);
    detail.Dispose();
    var changes = 0;
    detail.PropertyChanged += (_, _) => changes++;

    parent.OnUiLocaleChanged();
    await detail.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(0, changes);
    Assert.Null(detail.Application);
  }

  private static CommunityApplicationResponse Detail() => JsonSerializer.Deserialize<CommunityApplicationResponse>(
      ApiFixtureLoader.LoadResponse("web.communities.application.default"), VouchaApiJson.Options)!;

  private sealed class MutableSession : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; private set; } = SessionSnapshotForTests.Authenticated;
    public void ChangeIdentity()
    {
      Current = new(new User("other-user", "other", Roles: ["user"]));
      SessionChanged?.Invoke(this, new(Current));
    }
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

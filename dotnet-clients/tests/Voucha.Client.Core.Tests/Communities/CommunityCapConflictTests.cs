using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityCapConflictTests
{
  private const string MembershipFixture = "native.communities.membership-cap.join.conflict";
  private const string ModeratorFixture = "native.communities.moderator-cap.promotion.conflict";

  [Theory]
  [InlineData(MembershipFixture, "COMMUNITY_MEMBERSHIP_LIMIT_REACHED")]
  [InlineData(ModeratorFixture, "COMMUNITY_MODERATOR_LIMIT_REACHED")]
  public async Task TransportPreservesCapStatusAndCodeWithoutRetry(string fixture, string expectedCode)
  {
    var body = ApiFixtureLoader.LoadResponse(fixture);
    using var handler = new RecordingHandler(body, HttpStatusCode.Conflict);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiCommunitiesService(new VouchaApiClient(http));

    var error = await Assert.ThrowsAsync<VouchaApiException>(() => fixture == MembershipFixture
        ? service.JoinAsync("test-community", TestContext.Current.CancellationToken)
        : service.UpdateMemberRoleAsync("test-community", "user-2", "moderator", TestContext.Current.CancellationToken));

    Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
    Assert.Equal(expectedCode, error.ErrorCode);
    Assert.Equal(body, error.ResponseBody);
    Assert.Single(handler.Requests);
  }

  [Theory]
  [InlineData(CommunityActionKind.Create)]
  [InlineData(CommunityActionKind.RedeemInvite)]
  public async Task SubmissionShowsMembershipGuidanceWithoutRetry(CommunityActionKind kind)
  {
    var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse(MembershipFixture), HttpStatusCode.Conflict);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    using var model = new CommunityActionViewModel(new ApiCommunitiesService(new VouchaApiClient(http)), kind);
    model.Name = "Test Community Name";
    model.TurnstileToken = "token";
    model.Code = "invite-code";

    Assert.False(await model.SubmitAsync(TestContext.Current.CancellationToken));
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeCommunitiesMembershipLimitReached), model.ErrorMessage);
    Assert.Equal(LoadState.Error, model.State);
    Assert.Single(handler.Requests);
    if (kind == CommunityActionKind.Create) Assert.Empty(model.TurnstileToken);
  }

  [Theory]
  [InlineData("join", MembershipFixture, false)]
  [InlineData("approve", MembershipFixture, false)]
  [InlineData("moderator", ModeratorFixture, true)]
  [InlineData("owner", ModeratorFixture, true)]
  [InlineData("transfer", ModeratorFixture, true)]
  public async Task DetailActionsShowDistinctGuidanceWithoutRetry(string action, string fixture, bool moderator)
  {
    using var handler = new CapHandler(fixture);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(new VouchaApiClient(http)), new Session());
    await model.LoadAsync("test-community", TestContext.Current.CancellationToken);
    Assert.False(model.HasError);
    var reads = handler.Reads;
    var result = action switch
    {
      "join" => await model.JoinAsync(TestContext.Current.CancellationToken),
      "approve" => await model.ReviewApplicationAsync("application-1", "approved", cancellationToken: TestContext.Current.CancellationToken),
      "transfer" => await model.TransferOwnershipAsync("user-2", TestContext.Current.CancellationToken),
      _ => await model.UpdateMemberRoleAsync("user-2", action, TestContext.Current.CancellationToken),
    };

    Assert.False(result);
    Assert.Equal(UiLocalization.English.Localize(moderator
        ? UiMessageKey.NativeCommunitiesModeratorLimitReached
        : UiMessageKey.NativeCommunitiesMembershipLimitReached), model.ErrorMessage);
    Assert.Equal(LoadState.Error, model.State);
    Assert.Equal(1, handler.Writes);
    Assert.Equal(reads, handler.Reads);
  }

  [Theory]
  [InlineData(HttpStatusCode.Conflict, "OTHER_CONFLICT")]
  [InlineData(HttpStatusCode.BadRequest, "COMMUNITY_MEMBERSHIP_LIMIT_REACHED")]
  public async Task UnexpectedConflictOrStatusUsesExistingGenericError(HttpStatusCode status, string code)
  {
    var handler = new RecordingHandler(System.Text.Json.JsonSerializer.Serialize(new { code, message = "Unexpected failure" }), status);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    using var model = new CommunityActionViewModel(new ApiCommunitiesService(new VouchaApiClient(http)), CommunityActionKind.RedeemInvite);
    model.Code = "invite-code";

    Assert.False(await model.SubmitAsync(TestContext.Current.CancellationToken));
    Assert.Equal($"Voucha API request failed with HTTP {(int)status}.", model.ErrorMessage);
    Assert.Single(handler.Requests);
  }

  private sealed class CapHandler(string fixture) : HttpMessageHandler
  {
    public int Writes { get; private set; }
    public int Reads { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      if (request.Method != HttpMethod.Get)
      {
        Writes++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
        { Content = new StringContent(ApiFixtureLoader.LoadResponse(fixture)) });
      }
      Reads++;
      var path = request.RequestUri!.AbsolutePath;
      var id = path.EndsWith("/members", StringComparison.Ordinal) ? "web.communities.members.default"
          : path.EndsWith("/posts", StringComparison.Ordinal) ? "web.communities.posts.default"
          : path.EndsWith("/counts", StringComparison.Ordinal) ? "web.communities.list-items.counts.default"
          : "web.communities.show.default";
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      { Content = new StringContent(ApiFixtureLoader.LoadResponse(id)) });
    }
  }

  private sealed class Session : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User(Id: "user-1", Username: "testuser", Roles: ["user"], EmailAddress: "tests@example.com", MembershipPlan: "free"));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

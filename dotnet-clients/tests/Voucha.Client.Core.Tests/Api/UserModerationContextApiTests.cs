using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class UserModerationContextApiTests
{
  [Theory]
  [InlineData("native.moderation.context.member", false)]
  [InlineData("native.moderation.context.staff", true)]
  public async Task FetchPreservesPublishedContextPrivacyAndNotes(string fixture, bool staff)
  {
    using var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse(fixture));
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var client = new VouchaApiClient(http);
    var userId = ApiFixtureLoader.RouteParameterValue(fixture, "userId");

    var response = await client.FetchUserModerationContextAsync(userId, TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Get, handler.Method);
    Assert.Equal($"/api/v1/users/{userId}/moderation-context", handler.PathAndQuery);
    Assert.Null(handler.RequestBody);
    Assert.Equal(86400000, response.Context.AccountAgeMs);
    Assert.Null(response.Context.ActiveSuspension);
    Assert.Equal(staff ? 1 : (int?)null, response.Context.TrustTier);
    Assert.Equal(staff ? 1000 : (int?)null, response.Context.ContentRemovalCount);
    Assert.Equal(staff ? true : (bool?)null, response.Context.ContentRemovalCountCapped);
    Assert.Equal(staff ? 2 : (int?)null, response.Context.CommunityRemovalCount);
    Assert.False(response.PageInfo.HasNextPage);
    var note = Assert.Single(response.Notes);
    Assert.Equal("00000000-0000-7000-8000-000000000912", note.Id);
    Assert.Equal(userId, note.TargetUserId);
    Assert.Equal("00000000-0000-7000-8000-000000000913", note.AuthorUserId);
    Assert.Equal("moderator", note.AuthorUsername);
    Assert.Null(note.AuthorDisplayName);
    Assert.Equal("00000000-0000-7000-8000-000000000914", note.CommunityId);
    Assert.Equal("Fixture community", note.CommunityName);
    Assert.Equal("Observed repeated spam.", note.Body);
    Assert.Equal("2026-06-01T12:00:00.000Z", note.CreatedAt);
    Assert.Null(note.DeletedAt);
  }

  [Fact]
  public void EndpointEscapesUserIdAsOnePathSegment()
  {
    Assert.Equal("/api/v1/users/user%2Fid/moderation-context",
        VouchaApiEndpoints.UserModerationContext("user/id").Path);
  }
}

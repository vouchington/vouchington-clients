using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed class ApiSettingsServiceTests
{
  [Fact]
  public async Task FetchMembershipAsyncReturnsNullForMissingMembership()
  {
    var (service, handler) = CreateService(new RecordedResponse("{}", HttpStatusCode.NotFound));

    var membership = await service.FetchMembershipAsync(TestContext.Current.CancellationToken);

    Assert.Null(membership);
    Assert.Equal("/api/v1/memberships/me", handler.PathAndQuery);
  }

  [Fact]
  public async Task FetchUserDataRequestAsyncReturnsNullForMissingRequest()
  {
    var (service, handler) = CreateService(new RecordedResponse("{}", HttpStatusCode.NotFound));

    var request = await service.FetchUserDataRequestAsync("user-1", TestContext.Current.CancellationToken);

    Assert.Null(request);
    Assert.Equal("/api/v1/users/user-1/data-request", handler.PathAndQuery);
  }

  [Fact]
  public async Task CreateApiKeyAsyncTrimsTheLabelAndForwardsExplicitPermissions()
  {
    var (service, handler) = CreateService(new RecordedResponse(CreateApiKeyResponseJson()));

    await service.CreateApiKeyAsync("  Reader  ", "rss", ["rss:read"], TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal("/api/v1/my/api-keys", handler.PathAndQuery);
    Assert.Contains("\"label\":\"Reader\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.Contains("\"type\":\"rss\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.Contains("\"permissions\":[\"rss:read\"]", handler.RequestBody, StringComparison.Ordinal);
  }

  [Fact]
  public async Task FetchAuthSessionsAsyncUsesTheAuthSessionsRoute()
  {
    var responseJson = """
      {
        "results": [
          {
            "id": "session-1",
            "device_id": "device-1",
            "device_name": "MacBook Pro",
            "user_agent": "Safari",
            "ip_address": "203.0.113.8",
            "created_at": "2026-07-01T12:00:00Z",
            "last_seen_at": "2026-07-01T13:00:00Z",
            "expires_at": "2026-07-31T12:00:00Z",
            "is_current": true
          }
        ],
        "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null }
      }
      """;
    var (service, handler) = CreateService(new RecordedResponse(responseJson));

    var sessions = await service.FetchAuthSessionsAsync(TestContext.Current.CancellationToken);

    Assert.Single(sessions.Results);
    Assert.Equal("/api/v1/auth/sessions", handler.PathAndQuery);
  }

  [Fact]
  public async Task PaginationWrappersForwardOpaqueCursorsAndLimits()
  {
    const string EmptyPage = """
      {
        "results": [],
        "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null }
      }
      """;
    var (service, handler) = CreateService(
        new RecordedResponse(EmptyPage),
        new RecordedResponse(EmptyPage),
        new RecordedResponse(EmptyPage));

    await service.FetchAuthSessionsPageAsync(
        "sessions/opaque+1", 17, TestContext.Current.CancellationToken);
    await service.FetchApiKeysPageAsync(
        "keys/opaque+2", 19, TestContext.Current.CancellationToken);
    await service.FetchPushSubscriptionsPageAsync(
        "push/opaque+3", 23, TestContext.Current.CancellationToken);

    Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    Assert.Collection(
        handler.Requests,
        request => Assert.Equal(
            "/api/v1/auth/sessions?after=sessions%2Fopaque%2B1&limit=17",
            request.PathAndQuery),
        request => Assert.Equal(
            "/api/v1/my/api-keys?after=keys%2Fopaque%2B2&limit=19",
            request.PathAndQuery),
        request => Assert.Equal(
            "/api/v1/my/notifications/push-subscriptions?after=push%2Fopaque%2B3&limit=23",
            request.PathAndQuery));
  }

  [Fact]
  public async Task DeleteAuthSessionAsyncUsesTheSessionRoute()
  {
    var (service, handler) = CreateService(new RecordedResponse("{}", HttpStatusCode.NoContent));

    await service.DeleteAuthSessionAsync("session-1", TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Delete, handler.Method);
    Assert.Equal("/api/v1/auth/sessions/session-1", handler.PathAndQuery);
  }

  [Fact]
  public async Task RevokeAuthSessionsAsyncUsesTheRevokeAllRoute()
  {
    var (service, handler) = CreateService(new RecordedResponse("{}", HttpStatusCode.NoContent));

    await service.RevokeAuthSessionsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal("/api/v1/auth/sessions/revocations", handler.PathAndQuery);
  }

  [Fact]
  public async Task CreateApiKeyAsyncDoesNotExpandExplicitMcpPermissions()
  {
    var (service, handler) = CreateService(new RecordedResponse(CreateApiKeyResponseJson()));

    await service.CreateApiKeyAsync("Tools", "mcp", ["mcp.user:read"], TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal("/api/v1/my/api-keys", handler.PathAndQuery);
    Assert.Contains("\"type\":\"mcp\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.Contains("\"permissions\":[\"mcp.user:read\"]", handler.RequestBody, StringComparison.Ordinal);
  }

  private static (ApiSettingsService Service, RecordingHandler Handler) CreateService(
      params RecordedResponse[] responses)
  {
    var handler = new RecordingHandler(responses);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    return (new ApiSettingsService(client), handler);
  }

  private static string CreateApiKeyResponseJson() =>
      """
      {
        "api_key": {
          "id": "api-key-1",
          "user_id": "user-1",
          "prefix": "rk_abc123",
          "type": "rss",
          "label": "Reader",
          "permissions": ["rss:read"],
          "created_at": "2026-07-01T12:00:00Z",
          "last_used_at": null,
          "revoked_at": null,
          "updated_at": "2026-07-01T12:00:00Z"
        },
        "raw_key": "raw-key"
      }
      """;
}

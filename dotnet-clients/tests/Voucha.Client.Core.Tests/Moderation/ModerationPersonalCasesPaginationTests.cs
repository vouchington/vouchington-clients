using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Tests.Api;
using System.Text.Json;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationPersonalCasesPaginationTests
{
  [Fact]
  public async Task LegacyModerationCaseCancellationTokenPositionRemainsSupported()
  {
    var handler = new RecordingHandler("""{"results":[]}""");
    var client = Client(handler);

    await client.FetchModerationCaseCountAsync(
        "/api/v1/my/warnings", 25, TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/my/warnings?limit=25", handler.Requests.Single().PathAndQuery);
  }

  [Theory]
  [InlineData("/my/bans", "ban-1", "ban-2", "ban-cursor")]
  [InlineData("/my/removed-posts", "post-1", "post-2", "removed-post-cursor")]
  public async Task PersonalCasePagesUseBackendEnvelopeAndForwardAfter(
      string path,
      string firstId,
      string secondId,
      string cursor)
  {
    var handler = new RecordingHandler([
      new RecordedResponse(Page(path, firstId, cursor)),
      new RecordedResponse(Page(path, secondId, null)),
    ]);
    var viewModel = new ModerationViewModel(new ApiModerationService(Client(handler)));
    Assert.True(ModerationRoutes.TryResolve(path, out var context));
    viewModel.SetContext(context);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.HasMore);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    var expectedIds = path == "/my/removed-posts"
        ? new[] { $"community:{firstId}", $"community:{secondId}" }
        : [firstId, secondId];
    Assert.Equal(expectedIds, viewModel.Items.Select(row => row.Id));
    var expectedQuery = path == "/my/removed-posts"
        ? $"/api/v1{path}?after={cursor}&include_platform=true&limit=25"
        : $"/api/v1{path}?after={cursor}&limit=25";
    Assert.Equal(expectedQuery, handler.Requests[^1].PathAndQuery);
    Assert.False(viewModel.HasMore);
  }

  [Fact]
  public void RemovedPostContractAcceptsLegacyAndPlatformCommunityShapes()
  {
    var response = JsonSerializer.Deserialize<PersonalRemovedPostsResponse>(
        """
        {
          "removed_posts": [
            {
              "post_id": "post-1",
              "post_title": "Legacy",
              "community_id": "community-1",
              "community_slug": "community-one",
              "unpublished_at": "2026-07-01T00:00:00Z",
              "post_removal_kind": "community"
            },
            {
              "post_id": "post-1",
              "post_title": "Platform",
              "community_id": null,
              "community_slug": null,
              "unpublished_at": "2026-07-01T00:00:00Z",
              "post_removal_kind": "platform"
            }
          ],
          "page_info": {"has_next_page": false, "start_cursor": null, "end_cursor": null}
        }
        """);

    Assert.NotNull(response);
    Assert.Equal("community-1", response.RemovedPosts[0].CommunityId);
    Assert.Null(response.RemovedPosts[1].CommunityId);
  }

  [Fact]
  public async Task RemovedPostRowsKeepPlatformAndCommunityDecisionsDistinct()
  {
    var communityPage = Page("/my/removed-posts", "post-1", "next");
    var platformPage = Page("/my/removed-posts", "post-1", null)
        .Replace("\"post_removal_kind\": \"community\"", "\"post_removal_kind\": \"platform\"");
    var handler = new RecordingHandler([
      new RecordedResponse(communityPage),
      new RecordedResponse(platformPage),
    ]);
    var viewModel = new ModerationViewModel(new ApiModerationService(Client(handler)));
    Assert.True(ModerationRoutes.TryResolve("/my/removed-posts", out var context));
    viewModel.SetContext(context);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["community:post-1", "platform:post-1"], viewModel.Items.Select(row => row.Id));
  }

  private static string Page(string path, string id, string? cursor) => path == "/my/bans"
      ? $$"""
          {
            "bans": [{
              "id": "{{id}}",
              "community_id": "community-1",
              "community_slug": "community-one",
              "user_id": "user-1",
              "reason": "Repeated abuse",
              "expires_at": null,
              "created_at": "2026-07-01T00:00:00Z",
              "updated_at": "2026-07-01T00:00:00Z",
              "lifted_at": null,
              "__entity_type": "community_ban"
            }],
            "page_info": {
              "has_next_page": {{(cursor is not null).ToString().ToLowerInvariant()}},
              "start_cursor": "start",
              "end_cursor": {{Json(cursor)}}
            }
          }
          """
      : $$"""
          {
            "removed_posts": [{
              "post_id": "{{id}}",
              "post_title": "Removed post {{id}}",
              "community_id": "community-1",
              "community_slug": "community-one",
              "unpublished_at": "2026-07-01T00:00:00Z",
              "post_removal_kind": "community",
              "__entity_type": "removed_post"
            }],
            "page_info": {
              "has_next_page": {{(cursor is not null).ToString().ToLowerInvariant()}},
              "start_cursor": "start",
              "end_cursor": {{Json(cursor)}}
            }
          }
          """;

  private static string Json(string? value) => value is null ? "null" : $"\"{value}\"";

  private static VouchaApiClient Client(HttpMessageHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
}

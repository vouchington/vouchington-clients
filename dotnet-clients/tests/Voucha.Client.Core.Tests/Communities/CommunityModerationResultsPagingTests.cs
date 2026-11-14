using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityModerationResultsPagingTests
{
  [Fact]
  public void FixtureDecodesEmptyPageAndKeepsPlatformStatus()
  {
    var response = JsonSerializer.Deserialize<CommunityModerationResultsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.moderation-results.default"),
        VouchaApiJson.Options)!;

    Assert.Empty(response.Results);
    Assert.False(response.PageInfo.HasNextPage);
    Assert.Null(response.PageInfo.EndCursor);
    Assert.Equal(AdminReviewQueueClearanceStatus.InReview, response.PlatformModeration.Status);
  }

  [Fact]
  public void SummaryRowsDecodeWithoutRawResultPayload()
  {
    var response = JsonSerializer.Deserialize<CommunityModerationResultsResponse>(
        """
        {
          "results": [{
            "id": "moderation-1",
            "post_id": "post-1",
            "community_prompt_id": "prompt-1",
            "is_flagged": true,
            "created_at": "2026-01-02T03:04:05.000Z"
          }],
          "page_info": { "end_cursor": "cursor-1", "has_next_page": true, "start_cursor": null },
          "platform_moderation": { "status": "approved" }
        }
        """,
        VouchaApiJson.Options)!;

    var row = Assert.Single(response.Results);
    Assert.Equal("moderation-1", row.Id);
    Assert.Equal("post-1", row.PostId);
    Assert.Equal("prompt-1", row.CommunityPromptId);
    Assert.True(row.IsFlagged);
    Assert.True(response.PageInfo.HasNextPage);
    Assert.Equal("cursor-1", response.PageInfo.EndCursor);
  }

  [Fact]
  public void EndpointForwardsOpaqueCursorAndLimitOnlyWhenPresent()
  {
    var first = VouchaApiEndpoints.CommunityModerationResults("test community", "post 1");
    var next = VouchaApiEndpoints.CommunityModerationResults("test community", "post 1", "cursor+/=", 100);

    Assert.Equal("/api/v1/communities/test%20community/posts/post%201/moderation-results", next.Path);
    Assert.Empty(first.Query);
    Assert.Equal(
        [
          new KeyValuePair<string, string>("after", "cursor+/="),
          new KeyValuePair<string, string>("limit", "100"),
        ],
        next.Query);
  }

  [Fact]
  public async Task CountFollowsEveryPageAndKeepsPlatformStatus()
  {
    List<string?> requestedCursors = [];
    var service = new ScriptedCommunitiesService
    {
      ModerationResultsHandler = (_, _, after, _) =>
      {
        requestedCursors.Add(after);
        return Task.FromResult(after switch
        {
          null => Page(AdminReviewQueueClearanceStatus.InReview, 100, "cursor-1"),
          "cursor-1" => Page(AdminReviewQueueClearanceStatus.InReview, 100, "cursor-2"),
          _ => Page(AdminReviewQueueClearanceStatus.Approved, 3, null),
        });
      },
    };
    var viewModel = await CreateLoadedViewModelAsync(service);

    var response = await viewModel.LoadModerationResultsAsync("post-1", TestContext.Current.CancellationToken);

    Assert.Equal([null, "cursor-1", "cursor-2"], requestedCursors);
    Assert.Equal(203, response!.Results.Count);
    Assert.Equal("203", viewModel.Moderation.Single(row => row.Id == "community-agent-moderations").SubtitleText.VerbatimValue);
    Assert.Equal(
        UiMessageKey.NativeDotnetModerationApproved,
        viewModel.Moderation.Single(row => row.Id == "platform-moderation").SubtitleText.Key);
  }

  [Fact]
  public async Task NextPageWithoutCursorOrWithRepeatedCursorFailsInsteadOfPartialCount()
  {
    var missing = new ScriptedCommunitiesService
    {
      ModerationResultsHandler = (_, _, _, _) =>
          Task.FromResult(Page(AdminReviewQueueClearanceStatus.Approved, 1, null, hasNextPage: true)),
    };
    var repeated = new ScriptedCommunitiesService
    {
      ModerationResultsHandler = (_, _, _, _) =>
          Task.FromResult(Page(AdminReviewQueueClearanceStatus.Approved, 1, "same", hasNextPage: true)),
    };

    foreach (var service in new[] { missing, repeated })
    {
      var viewModel = await CreateLoadedViewModelAsync(service);
      await Assert.ThrowsAsync<InvalidOperationException>(() =>
          viewModel.LoadModerationResultsAsync("post-1", TestContext.Current.CancellationToken));
      Assert.DoesNotContain(viewModel.Moderation, row => row.Id == "platform-moderation");
    }
  }

  [Fact]
  public async Task StaleLookupStopsPagingAndCannotOverwriteCurrentResult()
  {
    var staleFirstPage = new TaskCompletionSource<CommunityModerationResultsResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var stalePageRequests = 0;
    var service = new ScriptedCommunitiesService
    {
      ModerationResultsHandler = (_, postId, _, _) =>
      {
        if (postId != "stale-post")
        {
          return Task.FromResult(Page(AdminReviewQueueClearanceStatus.Approved, 2, null));
        }

        stalePageRequests++;
        return staleFirstPage.Task;
      },
    };
    var viewModel = await CreateLoadedViewModelAsync(service);

    var staleLoad = viewModel.LoadModerationResultsAsync("stale-post", TestContext.Current.CancellationToken);
    await viewModel.LoadModerationResultsAsync("current-post", TestContext.Current.CancellationToken);
    staleFirstPage.SetResult(Page(AdminReviewQueueClearanceStatus.Rejected, 1, "stale-cursor"));
    var staleResult = await staleLoad;

    Assert.Null(staleResult);
    Assert.Equal(1, stalePageRequests);
    Assert.Equal("2", viewModel.Moderation.Single(row => row.Id == "community-agent-moderations").SubtitleText.VerbatimValue);
    Assert.Equal(
        UiMessageKey.NativeDotnetModerationApproved,
        viewModel.Moderation.Single(row => row.Id == "platform-moderation").SubtitleText.Key);
  }

  private static async Task<CommunityDetailViewModel> CreateLoadedViewModelAsync(ScriptedCommunitiesService service)
  {
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: true, hasPendingApplication: false));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);
    return viewModel;
  }

  private static CommunityModerationResultsResponse Page(
      AdminReviewQueueClearanceStatus status,
      int count,
      string? endCursor,
      bool? hasNextPage = null) =>
      new(
          Enumerable.Range(0, count)
              .Select(i => new CommunityAgentModerationSummary(
                  $"result-{endCursor}-{i}", "post-1", "prompt-1", false, DateTimeOffset.UnixEpoch))
              .ToList(),
          new PageInfo(endCursor, hasNextPage ?? endCursor is not null, null),
          new CommunityPlatformModeration(status));
}

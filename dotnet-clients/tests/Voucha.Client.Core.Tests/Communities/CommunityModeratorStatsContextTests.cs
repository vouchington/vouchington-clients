using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityModeratorStatsContextTests
{
  [Fact]
  public async Task EarlierCommunityStatsCannotOverwriteReturnToSameCommunity()
  {
    var pending = new TaskCompletionSource<CommunityModeratorStatsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var firstA = true;
    var service = new ScriptedCommunitiesService
    {
      ModeratorStatsHandler = (community, _) =>
      {
        if (community == "community-a" && firstA)
        {
          firstA = false;
          firstStarted.SetResult();
          return pending.Task;
        }
        return Task.FromResult(Stats(community == "community-a" ? "fresh-a" : "community-b"));
      },
    };
    SeedLoadResponseSet(service, Detail(), Detail(), Detail());
    var viewModel = CreateViewModel(service);
    var staleLoad = viewModel.LoadSurfaceAsync("community-a", CommunityDetailSurfaceSection.Moderation, TestContext.Current.CancellationToken);
    try
    {
      await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      await viewModel.LoadSurfaceAsync("community-b", CommunityDetailSurfaceSection.Moderation, TestContext.Current.CancellationToken);
      await viewModel.LoadSurfaceAsync("community-a", CommunityDetailSurfaceSection.Moderation, TestContext.Current.CancellationToken);
      Assert.Contains(viewModel.Moderation, row => row.Id == "fresh-a");

      pending.SetResult(Stats("stale-a"));
      await staleLoad;

      Assert.Contains(viewModel.Moderation, row => row.Id == "fresh-a");
      Assert.DoesNotContain(viewModel.Moderation, row => row.Id == "stale-a");
      Assert.Equal("fresh-a-placement", viewModel.Moderation.Single(row => row.Id == "fresh-a").ProfileImagePlacement?.PlacementId);
    }
    finally
    {
      pending.TrySetResult(Stats("stale-a"));
      await staleLoad;
    }
  }

  [Theory]
  [InlineData(null)]
  [InlineData("http")]
  [InlineData("api")]
  [InlineData("invalid")]
  [InlineData("canceled")]
  public async Task SectionRoundTripRejectsLateStatsCompletionWithoutChangingCurrentRows(string? failure)
  {
    var pending = new TaskCompletionSource<CommunityModeratorStatsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var first = true;
    var service = new ScriptedCommunitiesService
    {
      ModeratorStatsHandler = (_, _) =>
      {
        if (first)
        {
          first = false;
          firstStarted.SetResult();
          return pending.Task;
        }
        return Task.FromResult(Stats("current"));
      },
    };
    SeedLoadResponseSet(service, Detail());
    var viewModel = CreateViewModel(service);
    var staleLoad = viewModel.LoadSurfaceAsync("community-a", CommunityDetailSurfaceSection.Moderation, TestContext.Current.CancellationToken);
    try
    {
      await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.Overview, TestContext.Current.CancellationToken);
      await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.Moderation, TestContext.Current.CancellationToken);
      Assert.Contains(viewModel.Moderation, row => row.Id == "current");

      if (failure is null)
        pending.SetResult(Stats("stale"));
      else
        pending.SetException(failure switch
        {
          "http" => new HttpRequestException("late stats failure"),
          "api" => new VouchaApiException(HttpStatusCode.InternalServerError, "{}"),
          "invalid" => new InvalidOperationException("late stats failure"),
          "canceled" => new OperationCanceledException("late stats failure"),
          _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        });
      await staleLoad;

      Assert.Equal(CommunityDetailSurfaceSection.Moderation, viewModel.SelectedSection);
      Assert.Contains(viewModel.Moderation, row => row.Id == "current");
      Assert.Equal(LoadState.Loaded, viewModel.State);
      Assert.Null(viewModel.ErrorMessage);
    }
    finally
    {
      pending.TrySetResult(Stats("stale"));
      await staleLoad;
    }
  }

  [Fact]
  public async Task EarlierCommunityQueueCannotClearCurrentModerationRows()
  {
    var pending = new TaskCompletionSource<CommunityModerationQueueResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var firstA = true;
    var currentQueue = JsonSerializer.Deserialize<CommunityModerationQueueResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.moderation-queue.default"), VouchaApiJson.Options)!;
    var service = new ScriptedCommunitiesService
    {
      ModerationQueueHandler = (community, _) =>
      {
        if (community == "community-a" && firstA)
        {
          firstA = false;
          firstStarted.SetResult();
          return pending.Task;
        }
        return Task.FromResult(currentQueue);
      },
    };
    SeedLoadResponseSet(service, Detail(), Detail(), Detail());
    var viewModel = CreateViewModel(service);
    var staleLoad = viewModel.LoadSurfaceAsync("community-a", CommunityDetailSurfaceSection.Moderation, TestContext.Current.CancellationToken);
    try
    {
      await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      await viewModel.LoadSurfaceAsync("community-b", CommunityDetailSurfaceSection.Moderation, TestContext.Current.CancellationToken);
      await viewModel.LoadSurfaceAsync("community-a", CommunityDetailSurfaceSection.Moderation, TestContext.Current.CancellationToken);
      Assert.Contains(viewModel.ModerationRows, row => row.Id == "queue-1");
      Assert.False(viewModel.HasMoreCommunityList);

      pending.SetResult(new CommunityModerationQueueResponse([], new PageInfo("stale-cursor", true, null), "moderator"));
      await staleLoad;

      Assert.Contains(viewModel.ModerationRows, row => row.Id == "queue-1");
      Assert.Contains(viewModel.Moderation, row => row.Id == "queue-1");
      Assert.False(viewModel.HasMoreCommunityList);
    }
    finally
    {
      pending.TrySetResult(new CommunityModerationQueueResponse([], new PageInfo(null, false, null), "moderator"));
      await staleLoad;
    }
  }

  private static CommunityDetailViewModel CreateViewModel(ScriptedCommunitiesService service) =>
      new(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

  private static CommunityResponse Detail() =>
      CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "moderator");

  private static CommunityModeratorStatsResponse Stats(string actor) =>
      new(30, [new CommunityModeratorStatEntry(actor, 1)], new Dictionary<string, PublicUser>
      {
        [actor] = new(actor, actor, ProfileImagePlacement: new TopicImagePlacement("image", actor + "-placement", 1)),
      });
}

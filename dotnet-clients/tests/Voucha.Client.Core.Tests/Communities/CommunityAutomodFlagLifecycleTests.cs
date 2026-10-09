using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Tests.Api;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityAutomodFlagLifecycleTests
{
  [Fact]
  public void VerifiedAutomodFixtureUsesPostEntityIdWithoutPostId()
  {
    var fixture = File.ReadAllText(FilamentsContractPaths.ApiFixture(
        "responses/native.communities.moderation-queue.automod-flag.page-1.json"));
    var response = JsonSerializer.Deserialize<CommunityModerationQueueResponse>(fixture, VouchaApiJson.Options)!;
    var flag = Assert.Single(response.Entries);

    Assert.Equal("automod_flag", flag.QueueSource);
    Assert.Equal("moderator", response.ViewerTier);
    Assert.Null(flag.PostId);
    Assert.Equal(flag.EntityId, CommunityAutomodFlagTarget.PostId(flag));
    Assert.NotNull(response.PageInfo.EndCursor);
  }

  [Fact]
  public async Task ModeratorListFiltersFutureSourcesAndForwardsOpaqueCursor()
  {
    var (model, service) = await ModeratorAsync();
    service.AutomodFlagResponses.Enqueue(Page([Entry("future", "new_source"), Entry("flag-1", "automod_flag")], "opaque", true));
    service.AutomodFlagResponses.Enqueue(Page([Entry("flag-2", "automod_flag")], null, false));

    await model.LoadAutomodFlagsAsync(TestContext.Current.CancellationToken);
    await model.LoadMoreAutomodFlagsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["flag-1", "flag-2"], model.AutomodFlags.Select(flag => flag.Id));
    Assert.Equal([null, "opaque"], service.AutomodFlagCursors);
    Assert.False(model.HasMoreAutomodFlags);
  }

  [Fact]
  public async Task MemberViewerTierHidesRowsEvenForLocalModerator()
  {
    var (model, service) = await ModeratorAsync();
    service.AutomodFlagResponses.Enqueue(Page([Entry("flag-1", "automod_flag")], null, false) with { ViewerTier = "member" });

    await model.LoadAutomodFlagsAsync(TestContext.Current.CancellationToken);

    Assert.Empty(model.AutomodFlags);
    Assert.False(model.HasMoreAutomodFlags);
    Assert.False(model.CanAutomaticallyLoadAutomodFlags);
  }

  [Fact]
  public async Task DismissWaitsForServerAndRejectsRepeatedTapThenRetainsFailure()
  {
    var (model, service) = await ModeratorAsync();
    service.AutomodFlagResponses.Enqueue(Page([Entry("flag-1", "automod_flag")], null, false));
    await model.LoadAutomodFlagsAsync(TestContext.Current.CancellationToken);
    var response = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.DismissAutomodFlagHandler = (_, _) => response.Task;

    var dismiss = model.DismissAutomodFlagAsync("post-1", TestContext.Current.CancellationToken);
    Assert.True(model.IsDismissingAutomodFlag("post-1"));
    await model.DismissAutomodFlagAsync("post-1", TestContext.Current.CancellationToken);
    Assert.Equal(1, service.AutomodDismissCalls);
    Assert.Single(model.AutomodFlags);
    response.SetException(new HttpRequestException("offline"));
    await dismiss;

    Assert.Single(model.AutomodFlags);
    Assert.NotNull(model.AutomodFlagError);
    Assert.False(model.IsDismissingAutomodFlag("post-1"));
  }

  [Fact]
  public async Task MissingFlagRefreshesInsteadOfShowingDismissError()
  {
    var (model, service) = await ModeratorAsync();
    service.AutomodFlagResponses.Enqueue(Page([Entry("flag-1", "automod_flag")], null, false));
    service.AutomodFlagResponses.Enqueue(Page([], null, false));
    await model.LoadAutomodFlagsAsync(TestContext.Current.CancellationToken);
    service.DismissAutomodFlagHandler = (_, _) => Task.FromException(new VouchaApiException(HttpStatusCode.NotFound, "{}"));

    await model.DismissAutomodFlagAsync("post-1", TestContext.Current.CancellationToken);

    Assert.Empty(model.AutomodFlags);
    Assert.Null(model.AutomodFlagError);
    Assert.Equal([null, null], service.AutomodFlagCursors);
  }

  [Fact]
  public async Task DismissedFlagAlsoLeavesUnfilteredModerationRows()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(true, false, membershipRole: "moderator"));
    service.ModerationQueueResponses.Enqueue(Page([
      Entry("flag-1", "automod_flag"), Entry("report-1", "report")], null, false));
    service.AutomodFlagResponses.Enqueue(Page([Entry("flag-1", "automod_flag")], null, false));
    var model = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));
    await model.LoadAsync("community-1", TestContext.Current.CancellationToken);
    await model.SelectSectionAsync(CommunityDetailSurfaceSection.Moderation, TestContext.Current.CancellationToken);

    Assert.Contains(model.ModerationRows, row => row.Id == "flag-1");
    Assert.Contains(model.Moderation, row => row.Id == "flag-1");
    await model.DismissAutomodFlagAsync("post-1", TestContext.Current.CancellationToken);

    Assert.Empty(model.AutomodFlags);
    Assert.DoesNotContain(model.ModerationRows, row => row.Id == "flag-1");
    Assert.DoesNotContain(model.Moderation, row => row.Id == "flag-1");
    Assert.Contains(model.ModerationRows, row => row.Id == "report-1");
  }

  [Fact]
  public async Task SettingsActionChangesOnlyAfterCommittedResponse()
  {
    var (model, service) = await ModeratorAsync();
    var oldAction = model.Community!.AutomodAction;
    var response = new TaskCompletionSource<CommunityResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.UpdateAutomodActionHandler = (_, _) => response.Task;

    var save = model.UpdateAutomodActionAsync("unpublish", TestContext.Current.CancellationToken);
    Assert.Equal(oldAction, model.Community.AutomodAction);
    response.SetResult(CreateDetailResponse(true, false, membershipRole: "moderator") with
    {
      Community = model.Community with { AutomodAction = "unpublish" },
    });
    Assert.True(await save);
    Assert.Equal("unpublish", model.Community.AutomodAction);
    Assert.Equal("unpublish", service.SavedAutomodAction);
  }

  private static async Task<(CommunityDetailViewModel Model, ScriptedCommunitiesService Service)> ModeratorAsync()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(true, false, membershipRole: "moderator"));
    var model = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));
    await model.LoadAsync("community-1", TestContext.Current.CancellationToken);
    await model.SelectSectionAsync(CommunityDetailSurfaceSection.Moderation, TestContext.Current.CancellationToken);
    service.AutomodFlagCursors.Clear();
    return (model, service);
  }

  private static CommunityModerationQueueResponse Page(
      IReadOnlyList<CommunityModerationQueueEntry> entries, string? cursor, bool more) =>
      new(entries, new PageInfo(cursor, more, null), "moderator");

  private static CommunityModerationQueueEntry Entry(string id, string source) =>
      new(id, "community-1", "post", "post-1", null, "pending", source, null, null, 0,
          DateTimeOffset.Parse("2026-07-01T00:00:00Z"), DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
          "Flagged post", "/posts/post-1", true, false, false, null, null, null, null, null,
          null, "/posts/post-1", null, null, false, null, null, null);
}

internal sealed partial class ScriptedCommunitiesService
{
  public Queue<CommunityModerationQueueResponse> AutomodFlagResponses { get; } = [];
  public List<string?> AutomodFlagCursors { get; } = [];
  public Func<string, CancellationToken, Task>? DismissAutomodFlagHandler { get; set; }
  public Func<string, CancellationToken, Task<CommunityResponse>>? UpdateAutomodActionHandler { get; set; }
  public int AutomodDismissCalls { get; private set; }
  public string? SavedAutomodAction { get; private set; }

  public Task<CommunityModerationQueueResponse> FetchAutomodFlagPageAsync(
      string idOrSlug, string? after, int limit, CancellationToken cancellationToken = default)
  {
    AutomodFlagCursors.Add(after);
    return Task.FromResult(AutomodFlagResponses.Count > 0
        ? AutomodFlagResponses.Dequeue()
        : new CommunityModerationQueueResponse([], new PageInfo(null, false, null), "member"));
  }

  public Task DismissAutomodFlagAsync(string idOrSlug, string postId, CancellationToken cancellationToken = default)
  {
    AutomodDismissCalls++;
    return DismissAutomodFlagHandler?.Invoke(postId, cancellationToken) ?? Task.CompletedTask;
  }

  public Task<CommunityResponse> UpdateAutomodActionAsync(
      string idOrSlug, UpdateCommunityAutomodSettingsRequest request, CancellationToken cancellationToken = default)
  {
    SavedAutomodAction = request.AutomodAction;
    return UpdateAutomodActionHandler?.Invoke(request.AutomodAction, cancellationToken)
        ?? throw new InvalidOperationException("Automod settings test handler was not configured.");
  }
}

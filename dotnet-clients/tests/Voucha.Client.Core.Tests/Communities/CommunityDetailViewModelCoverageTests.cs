using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityDetailViewModelCoverageTests
{
  [Fact]
  public async Task LoadAsyncHydratesDerivedStateAndReloadsAfterJoin()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: false, hasPendingApplication: false),
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false),
        CreateDetailResponse(hasMembership: false, hasPendingApplication: false));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    Assert.True(viewModel.CanJoin);
    Assert.False(viewModel.CanLeave);
    Assert.False(viewModel.CanUnarchive);
    Assert.False(viewModel.IsArchived);
    Assert.True(viewModel.HasMembers);
    Assert.True(viewModel.HasPosts);
    Assert.Equal("user-1", viewModel.Members[0].DisplayName);
    Assert.Equal("fallback-title", viewModel.Posts[0].Title);
    Assert.Equal("post", viewModel.Posts[0].ProtocolPostType);
    Assert.Equal(UiMessageKey.NativeDotnetPostsPostTypePost, viewModel.Posts[0].PostTypeText.Key);
    Assert.Equal("Post", viewModel.Posts[0].PostType);
    Assert.Equal(0, viewModel.Posts[0].ReplyCount);

    Assert.True(await viewModel.JoinAsync(TestContext.Current.CancellationToken));
    Assert.True(await viewModel.LeaveAsync(TestContext.Current.CancellationToken));

    Assert.Contains(("join", "community-1"), service.MutationCalls);
    Assert.Contains(("leave", "community-1"), service.MutationCalls);
    Assert.Null(viewModel.Membership);
    Assert.True(viewModel.CanJoin);
    Assert.False(viewModel.CanLeave);
  }

  [Theory]
  [InlineData("member", false, false, false, true)]
  [InlineData("moderator", true, false, true, true)]
  [InlineData("owner", true, true, true, true)]
  public async Task LoadAsyncHydratesCommunityActionCapabilitiesFromMembership(
      string role,
      bool canModerate,
      bool canManage,
      bool canManageMembers,
      bool canUseModmail)
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: role));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    Assert.Equal(canModerate, viewModel.CanModerateCommunity);
    Assert.Equal(canManage, viewModel.CanManageCommunity);
    Assert.Equal(canManageMembers, viewModel.CanManageMembers);
    Assert.Equal(canUseModmail, viewModel.CanUseModmail);
  }

  [Fact]
  public async Task LoadAsyncAllowsSiteModeratorsToUseModmailWithoutCommunityManagement()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: false, hasPendingApplication: false));
    var session = new SessionSnapshot(new User(
        Id: "moderator-1",
        Username: "moderator",
        Roles: ["moderator"],
        EmailAddress: "moderator@example.com",
        MembershipPlan: "free"));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(session));

    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanModerateCommunity);
    Assert.False(viewModel.CanManageCommunity);
    Assert.False(viewModel.CanManageMembers);
    Assert.True(viewModel.CanUseModmail);
  }

  [Fact]
  public async Task LoadAsyncAllowsAdministratorsToManageCommunitiesWithoutMembership()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: false, hasPendingApplication: false));
    var session = new SessionSnapshot(new User(
        Id: "admin-1",
        Username: "admin",
        Roles: ["administrator"],
        EmailAddress: "admin@example.com",
        MembershipPlan: "free"));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(session));

    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    Assert.True(viewModel.CanModerateCommunity);
    Assert.True(viewModel.CanManageCommunity);
    Assert.False(viewModel.CanManageMembers);
  }

  [Fact]
  public async Task LoadAsyncBlocksJoinWhenPendingApplicationExists()
  {
    var service = new ScriptedCommunitiesService
    {
      MembersFailure = new HttpRequestException("members hidden"),
      PostsFailure = new HttpRequestException("posts hidden"),
      CountsFailure = new HttpRequestException("counts hidden"),
    };
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: false, hasPendingApplication: true));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasPendingApplication);
    Assert.False(viewModel.CanJoin);
    Assert.False(viewModel.HasError);
    Assert.Empty(viewModel.Members);
    Assert.Empty(viewModel.Posts);
  }

  [Theory]
  [InlineData("private")]
  [InlineData("unlisted")]
  public async Task LoadAsyncSkipsHiddenResourcesForHiddenNonMembers(string visibility)
  {
    var service = new ScriptedCommunitiesService
    {
      MembersFailure = new HttpRequestException("members hidden"),
      PostsFailure = new HttpRequestException("posts hidden"),
      CountsFailure = new HttpRequestException("counts hidden"),
    };
    SeedLoadResponseSet(service, CreateDetailResponse(
        hasMembership: false,
        hasPendingApplication: false,
        visibility: visibility));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasError);
    Assert.True(viewModel.CanJoin);
    Assert.Empty(viewModel.Members);
    Assert.Empty(viewModel.Posts);
    Assert.Null(viewModel.ListItemCounts);
  }

  [Fact]
  public async Task LoadAsyncKeepsPrivateResourcesVisibleForAdministrators()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(
        hasMembership: false,
        hasPendingApplication: false,
        visibility: "private"));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(new SessionSnapshot(new User(
            "user-1",
            "alice",
            Roles: ["administrator"],
            EmailAddress: "a@example.com",
            MembershipPlan: "free"))));

    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasError);
    Assert.True(viewModel.CanJoin);
    Assert.NotEmpty(viewModel.Members);
    Assert.NotEmpty(viewModel.Posts);
    Assert.NotNull(viewModel.ListItemCounts);
  }

  [Fact]
  public async Task ActionMethodsReturnFalseBeforeLoad()
  {
    var viewModel = new CommunityDetailViewModel(new ScriptedCommunitiesService(), new TestSessionStore(SessionSnapshotForTests.Authenticated));

    Assert.False(await viewModel.JoinAsync(TestContext.Current.CancellationToken));
    Assert.False(await viewModel.LeaveAsync(TestContext.Current.CancellationToken));
    Assert.False(await viewModel.ArchiveAsync(TestContext.Current.CancellationToken));
    Assert.False(await viewModel.UnarchiveAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task LoadAsyncRejectsBlankIdOrSlug()
  {
    var viewModel = new CommunityDetailViewModel(new ScriptedCommunitiesService(), new TestSessionStore(SessionSnapshotForTests.Authenticated));

    var exception = await Assert.ThrowsAsync<ArgumentException>(() => viewModel.LoadAsync(" ", TestContext.Current.CancellationToken));

    Assert.Equal("idOrSlug", exception.ParamName);
  }

  [Fact]
  public async Task LoadAsyncCapturesHttpRequestFailures()
  {
    var failure = new HttpRequestException("community load failed");
    var service = new ScriptedCommunitiesService
    {
      DetailFailure = failure,
      MembersFailure = failure,
      PostsFailure = failure,
      CountsFailure = failure,
    };
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: false, hasPendingApplication: false));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.Equal("community load failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task LoadAsyncReturnsIdleAfterCancellation()
  {
    var failure = new OperationCanceledException();
    var service = new ScriptedCommunitiesService
    {
      DetailFailure = failure,
      MembersFailure = failure,
      PostsFailure = failure,
      CountsFailure = failure,
    };
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: false, hasPendingApplication: false));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task JoinAsyncReturnsFalseWhenMutationFails()
  {
    var service = new ScriptedCommunitiesService
    {
      JoinFailure = new OperationCanceledException(),
    };
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: false, hasPendingApplication: false));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    Assert.False(await viewModel.JoinAsync(TestContext.Current.CancellationToken));
    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.False(viewModel.HasError);
    Assert.Contains(("join", "community-1"), service.MutationCalls);
  }

  [Fact]
  public async Task UnarchiveAsyncReturnsFalseWhenMutationFails()
  {
    var service = new ScriptedCommunitiesService
    {
      UnarchiveFailure = new HttpRequestException("unarchive failed"),
    };
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(
            hasMembership: true,
            hasPendingApplication: false,
            archived: true,
            membershipRole: "owner"));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsArchived);
    Assert.False(await viewModel.UnarchiveAsync(TestContext.Current.CancellationToken));
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.Equal("unarchive failed", viewModel.ErrorMessage);
    Assert.NotNull(viewModel.Community);
    Assert.NotEmpty(viewModel.Members);
    Assert.NotEmpty(viewModel.Posts);
    Assert.Contains(("unarchive", "community-1"), service.MutationCalls);
  }

  [Fact]
  public async Task UnarchiveAsyncUpdatesArchivedState()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(
            hasMembership: true,
            hasPendingApplication: false,
            archived: true,
            membershipRole: "owner"));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsArchived);
    Assert.True(await viewModel.UnarchiveAsync(TestContext.Current.CancellationToken));
    Assert.Contains(("unarchive", "community-1"), service.MutationCalls);
    Assert.False(viewModel.IsArchived);
    Assert.True(viewModel.CanArchive);
    Assert.False(viewModel.CanUnarchive);
  }

  [Fact]
  public async Task LoadSelectedSectionAsyncHydratesManagementRows()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: true, hasPendingApplication: false));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Applications, TestContext.Current.CancellationToken);
    Assert.Equal("applicant-1", viewModel.Applications[0].Title);
    Assert.Equal("pending", viewModel.ApplicationRows[0].Status);

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.Invites, TestContext.Current.CancellationToken);
    Assert.Equal("person@example.com", viewModel.Invites[0].Title);
    Assert.Equal("pending", viewModel.InviteRows[0].Status);

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.Bans, TestContext.Current.CancellationToken);
    Assert.Equal("banned-user", viewModel.Moderation[0].Title);
    Assert.Equal("active", viewModel.BanRows[0].Status);

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.Restrictions, TestContext.Current.CancellationToken);
    Assert.Equal("raid_mode", viewModel.Moderation[0].Title);
    Assert.Equal("active", viewModel.RestrictionRows[0].Status);

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.ModeratorVacation, TestContext.Current.CancellationToken);
    Assert.Equal("Moderator Vacation", viewModel.Moderation[0].Title);
    Assert.NotNull(viewModel.ModeratorVacation);

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.AiAgents, TestContext.Current.CancellationToken);
    Assert.Equal("spam-guardian", viewModel.Moderation[0].Title);
    Assert.Equal("Enabled", viewModel.Moderation[0].Subtitle);
    Assert.Equal("spam, quality", viewModel.Moderation[0].Detail);
    Assert.Single(viewModel.AiAgents);

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.AgentPrompts, TestContext.Current.CancellationToken);
    Assert.Equal("agent-1", viewModel.Moderation[0].Title);
    Assert.Equal("Allocated", viewModel.Moderation[0].Subtitle);
    Assert.Single(viewModel.AgentPrompts);

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.Moderation, TestContext.Current.CancellationToken);
    Assert.Contains(viewModel.Moderation, row => row.Title == "user-1" && row.Detail == "2");
    Assert.Contains(viewModel.Moderation, row => row.Title == "post-1" && row.Detail == "reported");
    Assert.Single(viewModel.ModerationRows);
  }

  [Fact]
  public async Task LoadSelectedSectionAsyncHandlesEmptyModeratorVacation()
  {
    var service = new ScriptedCommunitiesService();
    service.ModeratorVacationResponses.Enqueue(new(null));
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: true, hasPendingApplication: false));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModeratorVacation, TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ModeratorVacation);
    Assert.Empty(viewModel.Moderation);
  }

  [Fact]
  public async Task ManagementActionsRecordMutationsAndReloadSelectedSection()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: true, hasPendingApplication: false));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Applications, TestContext.Current.CancellationToken);

    Assert.True(await viewModel.AddListItemAsync(new(TopicId: "topic-1"), TestContext.Current.CancellationToken));
    Assert.True(await viewModel.RemoveListItemAsync("topic", "topic-1", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.UpdateMemberRoleAsync("user-2", "moderator", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.RemoveMemberAsync("user-2", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.TransferOwnershipAsync("user-3", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.ReviewApplicationAsync("application-1", "approved", "ok", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.RevokeInviteAsync("invite-1", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.UpdatePinnedPostsAsync(["post-1"], TestContext.Current.CancellationToken));
    Assert.True(await viewModel.BanAsync("user-4", "spam", cancellationToken: TestContext.Current.CancellationToken));
    Assert.True(await viewModel.LiftBanAsync("user-4", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.ActivateRestrictionsAsync(["raid_mode"], reason: "spike", cancellationToken: TestContext.Current.CancellationToken));
    Assert.True(await viewModel.LiftRestrictionAsync("restriction-1", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.SetModeratorVacationAsync(DateTimeOffset.Parse("2026-07-08T00:00:00Z"), TestContext.Current.CancellationToken));
    Assert.True(await viewModel.SetSuppressCommunityDigestsWhileOnVacationAsync(true, TestContext.Current.CancellationToken));
    Assert.True(await viewModel.ClearModeratorVacationAsync(TestContext.Current.CancellationToken));
    Assert.True(await viewModel.ClaimModerationReportAsync("report-1", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.ReleaseModerationReportAsync("report-1", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.ReviewPostAsync("post-1", "approved", "ok", TestContext.Current.CancellationToken));

    Assert.Contains(("list-add", "community-1"), service.MutationCalls);
    Assert.Contains(("application-review", "community-1"), service.MutationCalls);
    Assert.Contains(("post-review", "community-1"), service.MutationCalls);
    Assert.Contains("application-1:approved:ok", service.MutationDetails);
    Assert.Contains("True", service.MutationDetails);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task AdminSurfaceMethodsLoadAndMutateTheModerationSurface()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: true, hasPendingApplication: false));
    service.SavedRepliesResponses.Enqueue(JsonSerializer.Deserialize<CommunitySavedRepliesResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.saved-replies.default"),
        VouchaApiJson.Options)!);
    service.AutomodRecentActionsResponses.Enqueue(JsonSerializer.Deserialize<CommunityAutomodActionsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.automod-recent-actions.default"),
        VouchaApiJson.Options)!);
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Settings, TestContext.Current.CancellationToken);
    Assert.Equal("Greeting", viewModel.Moderation[0].Title);

    await viewModel.LoadAutomodRecentActionsAsync(cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal("Fixture post", viewModel.Moderation[0].Title);

    Assert.True(await viewModel.UpdatePostTypeSettingsAsync(true, true, TestContext.Current.CancellationToken));
    Assert.True(await viewModel.IssueWarningAsync(
        "user-1",
        "Spam in community",
        "Please read the community rules.",
        resolveReport: false,
        cancellationToken: TestContext.Current.CancellationToken));
    Assert.True(await viewModel.ResolveModerationReportAsync("report-1", "resolved", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.ConfirmBanEvasionAsync("user-1", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.DismissBanEvasionAsync("user-1", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.OpenModmailAsync("user-2", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.OpenModmailForReportAsync("report-2", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.SendModmailMessageAsync("thread-1", "Hello", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.UpdateModmailThreadAsync("thread-1", "moderator-1", true, TestContext.Current.CancellationToken));
    Assert.True(await viewModel.EnableAiAgentAsync("spam-filter", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.DisableAiAgentAsync("spam-filter", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.CreateAgentPromptAsync("Prompt body", "gpt-5.4-nano", "openai", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.UpdateAgentPromptAsync("prompt-2", "Updated prompt", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.CreateSavedReplyAsync("Greeting", "Thanks for writing in.", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.DeleteSavedReplyAsync("saved-reply-1", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.DeleteAgentPromptAsync("prompt-2", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.AllocateAgentPromptSlotAsync("prompt-2", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.DeallocateAgentPromptSlotAsync("prompt-2", TestContext.Current.CancellationToken));
    var testRun = await viewModel.TestAgentPromptAsync(
        "prompt-2",
        "Body",
        saveForTraining: true,
        expectedFlagged: false,
        cancellationToken: TestContext.Current.CancellationToken);
    var results = await viewModel.LoadModerationResultsAsync("post-1", TestContext.Current.CancellationToken);

    Assert.NotNull(results);
    Assert.Equal(UiMessageKey.NativeDotnetModerationInReview, viewModel.Moderation[1].SubtitleText.Key);
    Assert.Equal(System.Text.Json.JsonValueKind.Object, testRun.ValueKind);
    Assert.False(testRun.GetProperty("flagged").GetBoolean());
    Assert.Contains(("post-type-settings", "community-1"), service.MutationCalls);
    Assert.Contains(("warning", "community-1"), service.MutationCalls);
    Assert.Contains(("resolve-report", "community-1"), service.MutationCalls);
    Assert.Contains(("modmail-open", "community-1"), service.MutationCalls);
    Assert.Contains(("agent-prompt-create", "community-1"), service.MutationCalls);
    Assert.Contains(("ban-evasion-confirm", "community-1"), service.MutationCalls);
    Assert.Contains("report-1:resolved", service.MutationDetails);
    Assert.Contains("prompt-2:Updated prompt", service.MutationDetails);
  }

  [Fact]
  public async Task RoutedModmailMutationsRejectBlankConversationIds()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: true, hasPendingApplication: false));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    Assert.False(await viewModel.SendRoutedModmailMessageAsync("", "Hello", null, TestContext.Current.CancellationToken));
    Assert.False(await viewModel.UpdateRoutedModmailThreadAsync(" ", "moderator-1", true, null, TestContext.Current.CancellationToken));
    Assert.Equal("Conversation id is required.", viewModel.ErrorMessage);
    Assert.Equal(LoadState.Error, viewModel.State);
  }

}

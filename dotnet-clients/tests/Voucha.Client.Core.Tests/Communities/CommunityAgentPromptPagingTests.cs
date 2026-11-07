using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityAgentPromptPagingTests
{
  private static CancellationToken Token => TestContext.Current.CancellationToken;

  [Fact]
  public async Task ShortAndEmptyPagesFollowServerCursorDeduplicateAndRetainGlobalSlots()
  {
    var fixture = Fixture();
    var service = Service();
    service.AgentPromptsResponses.Enqueue(Page(fixture, "first/+?", true));
    service.AgentPromptsResponses.Enqueue(Page(fixture with { CommunityAgentPrompts = [] }, "second/+?", true));
    var extra = fixture.CommunityAgentPrompts[0] with { Id = "prompt-2", Prompt = "Second" };
    service.AgentPromptsResponses.Enqueue(Page(fixture with { CommunityAgentPrompts = [fixture.CommunityAgentPrompts[0], extra] }, "ignored", false));
    using var model = new CommunityDetailViewModel(service, new Session());
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    Assert.True(model.HasMoreCommunityList);
    Assert.Equal(9, model.AgentPromptSlots?.Used);
    await model.LoadMoreCommunityListAsync(Token);
    Assert.True(model.HasMoreCommunityList);
    Assert.Single(model.AgentPrompts);
    await model.LoadMoreCommunityListAsync(Token);
    Assert.Equal(["prompt-1", "prompt-2"], model.AgentPrompts.Select(prompt => prompt.Id));
    Assert.Equal(["prompt-1", "prompt-2"], model.Moderation.Select(row => row.Id));
    Assert.False(model.HasMoreCommunityList);
    Assert.Equal(new CommunityAgentPromptSlotInfo(9, 10, 1, new(3, 10)), model.AgentPromptSlots);
    Assert.Equal(new string?[] { null, "first/+?", "second/+?" }, service.ForwardPageRequests.Select(request => request.After));
    Assert.All(service.ForwardPageRequests, request => Assert.Equal(25, request.Limit));
  }

  [Fact]
  public async Task ContinuationFailureRetriesSameCursorAndReloadRejectsOlderPage()
  {
    var service = Service();
    var fixture = Page(Fixture(), "next", true);
    service.AgentPromptsResponses.Enqueue(fixture);
    using var model = new CommunityDetailViewModel(service, new Session());
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    service.AgentPromptPageHandler = _ => Task.FromException<CommunityAgentPromptsResponse>(new HttpRequestException("offline"));
    await model.LoadMoreCommunityListAsync(Token);
    Assert.Equal("offline", model.CommunityListPaginationErrorMessage);
    Assert.Single(model.AgentPrompts);
    var delayed = new TaskCompletionSource<CommunityAgentPromptsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptPageHandler = _ => delayed.Task;
    var oldPage = model.LoadMoreCommunityListAsync(Token);
    Assert.Equal("next", service.ForwardPageRequests[^1].After);
    service.AgentPromptPageHandler = _ => Task.FromResult(Page(Fixture() with { CommunityAgentPrompts = [] }, null, false));
    await model.LoadAgentPromptsAsync(Token);
    delayed.SetResult(fixture);
    await oldPage;
    Assert.Empty(model.AgentPrompts);
    Assert.Null(model.CommunityListPaginationErrorMessage);
    Assert.False(model.HasMoreCommunityList);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task NewerInitialLoadAndAccountChangeRejectOldCompletion(bool changeAccount)
  {
    var service = Service();
    var session = new Session();
    using var model = new CommunityDetailViewModel(service, session);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var delayed = new TaskCompletionSource<CommunityAgentPromptsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptPageHandler = _ => delayed.Task;
    var stale = model.LoadAgentPromptsAsync(Token);
    Assert.False(await model.CreateAgentPromptAsync("not sent", cancellationToken: Token));
    await model.LoadMoreCommunityListAsync(Token);
    if (changeAccount) session.Change();
    else
    {
      service.AgentPromptPageHandler = _ => Task.FromResult(Page(Fixture() with { CommunityAgentPrompts = [] }, null, false));
      await model.LoadAgentPromptsAsync(Token);
    }
    if (changeAccount) delayed.SetException(new HttpRequestException("old account offline"));
    else delayed.SetResult(Page(Fixture(), "stale", true));
    await stale;
    Assert.Empty(model.AgentPrompts);
    Assert.False(model.HasMoreCommunityList);
    Assert.Null(model.AgentPromptCreationError);
  }

  [Theory]
  [InlineData("Your community prompt limit has been reached.")]
  [InlineData("")]
  public async Task ExpectedCreationCapPreservesRowsSlotsCursorAndAllowsRetry(string message)
  {
    var service = Service();
    service.AgentPromptsResponses.Enqueue(Page(Fixture(), "next", true));
    using var model = new CommunityDetailViewModel(service, new Session());
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var slots = model.AgentPromptSlots;
    service.AgentPromptCreateHandler = _ => Task.FromException<CommunityAgentPrompt>(new VouchaApiException(HttpStatusCode.Conflict,
        JsonSerializer.Serialize(new { code = "COMMUNITY_AGENT_PROMPT_LIMIT_REACHED", message })));
    Assert.False(await model.CreateAgentPromptAsync("preserve draft", cancellationToken: Token));
    Assert.True(model.HasReachedAgentPromptLimit);
    Assert.Equal(string.IsNullOrEmpty(message) ? UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpActionFailed) : message, model.AgentPromptCreationError);
    Assert.Single(model.AgentPrompts);
    Assert.Equal(slots, model.AgentPromptSlots);
    Assert.True(model.HasMoreCommunityList);
    Assert.False(model.HasError);
    service.AgentPromptCreateHandler = _ => Task.FromResult(Fixture().CommunityAgentPrompts[0]);
    Assert.True(await model.CreateAgentPromptAsync("preserve draft", cancellationToken: Token));
    Assert.False(model.HasReachedAgentPromptLimit);
    Assert.Null(model.AgentPromptCreationError);
    Assert.Null(service.ForwardPageRequests[^1].After);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task DisposalAndAccountChangeIgnoreLateCreationConflict(bool dispose)
  {
    var service = Service();
    var session = new Session();
    using var model = new CommunityDetailViewModel(service, session);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var delayed = new TaskCompletionSource<CommunityAgentPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptCreateHandler = _ => delayed.Task;
    var creating = model.CreateAgentPromptAsync("draft", cancellationToken: Token);
    Assert.False(await model.CreateAgentPromptAsync("duplicate", cancellationToken: Token));
    if (dispose) model.Dispose();
    else
    {
      session.Change();
      Assert.False(model.IsLoading);
      service.AgentPromptCreateHandler = _ => Task.FromResult(Fixture().CommunityAgentPrompts[0]);
      Assert.True(await model.CreateAgentPromptAsync("new account draft", cancellationToken: Token));
    }
    delayed.SetException(new VouchaApiException(HttpStatusCode.Conflict, "{\"code\":\"COMMUNITY_AGENT_PROMPT_LIMIT_REACHED\",\"message\":\"stale\"}"));
    Assert.False(await creating);
    Assert.Null(model.AgentPromptCreationError);
    Assert.False(model.HasReachedAgentPromptLimit);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CanceledAndUnrelatedFailedCreationReleaseBusyStateForRetry(bool canceled)
  {
    var service = Service();
    using var model = new CommunityDetailViewModel(service, new Session());
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    service.AgentPromptCreateHandler = _ => Task.FromException<CommunityAgentPrompt>(canceled
        ? new OperationCanceledException(Token) : new VouchaApiException(HttpStatusCode.Conflict, "{\"code\":\"OTHER_CONFLICT\",\"message\":\"Other\"}"));
    Assert.False(await model.CreateAgentPromptAsync("draft", cancellationToken: Token));
    Assert.False(model.IsLoading);
    Assert.False(model.HasReachedAgentPromptLimit);
    Assert.Null(model.AgentPromptCreationError);
    Assert.Single(model.AgentPrompts);
    service.AgentPromptCreateHandler = _ => Task.FromResult(Fixture().CommunityAgentPrompts[0]);
    Assert.True(await model.CreateAgentPromptAsync("draft", cancellationToken: Token));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task LeavingPromptSectionReleasesItsCreationBusyStateAndIgnoresLateConflict(bool reloadPending)
  {
    var service = Service();
    using var model = new CommunityDetailViewModel(service, new Session());
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var delayed = new TaskCompletionSource<CommunityAgentPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptCreateHandler = _ => delayed.Task;
    var creating = model.CreateAgentPromptAsync("draft", cancellationToken: Token);
    var page = new TaskCompletionSource<CommunityAgentPromptsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    Task? reloading = null;
    if (reloadPending)
    {
      service.AgentPromptPageHandler = _ => page.Task;
      reloading = model.LoadAgentPromptsAsync(Token);
    }
    await model.SelectSectionAsync(CommunityDetailSurfaceSection.Overview, Token);
    Assert.False(model.IsLoading);
    delayed.SetException(new VouchaApiException(HttpStatusCode.Conflict, "{\"code\":\"COMMUNITY_AGENT_PROMPT_LIMIT_REACHED\",\"message\":\"old section\"}"));
    Assert.False(await creating);
    Assert.Null(model.AgentPromptCreationError);
    Assert.False(model.HasError);
    page.SetResult(Fixture());
    if (reloading is not null) await reloading;
    Assert.False(model.IsLoading);
  }

  [Fact]
  public async Task ReloadDuringPendingCreateDoesNotAdmitAnotherPost()
  {
    var service = Service();
    using var model = new CommunityDetailViewModel(service, new Session());
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var delayed = new TaskCompletionSource<CommunityAgentPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
    var posts = 0;
    service.AgentPromptCreateHandler = _ => { posts++; return delayed.Task; };
    var creating = model.CreateAgentPromptAsync("draft", cancellationToken: Token);
    await model.LoadAgentPromptsAsync(Token);
    Assert.False(await model.CreateAgentPromptAsync("duplicate", cancellationToken: Token));
    Assert.Equal(1, posts);
    delayed.SetResult(Fixture().CommunityAgentPrompts[0]);
    Assert.False(await creating);
    service.AgentPromptCreateHandler = _ => Task.FromResult(Fixture().CommunityAgentPrompts[0]);
    Assert.True(await model.CreateAgentPromptAsync("next draft", cancellationToken: Token));
  }

  [Fact]
  public async Task AccountChangeOffPromptPageSilentlyReleasesOldOperationWithoutClearingNewCreation()
  {
    var service = Service();
    var session = new Session();
    using var model = new CommunityDetailViewModel(service, session);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var oldPost = new TaskCompletionSource<CommunityAgentPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptCreateHandler = _ => oldPost.Task;
    var oldCreation = model.CreateAgentPromptAsync("old account draft", cancellationToken: Token);
    await model.SelectSectionAsync(CommunityDetailSurfaceSection.Overview, Token);
    var notifications = new List<string?>();
    model.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
    session.Change();
    Assert.Empty(notifications);
    await model.SelectSectionAsync(CommunityDetailSurfaceSection.AgentPrompts, Token);
    var newPost = new TaskCompletionSource<CommunityAgentPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
    var posts = 0;
    service.AgentPromptCreateHandler = _ => { posts++; return newPost.Task; };
    var newCreation = model.CreateAgentPromptAsync("new account draft", cancellationToken: Token);
    Assert.False(newCreation.IsCompleted);
    Assert.Equal(1, posts);
    oldPost.SetException(new VouchaApiException(HttpStatusCode.Conflict, "{\"code\":\"COMMUNITY_AGENT_PROMPT_LIMIT_REACHED\",\"message\":\"old account\"}"));
    Assert.False(await oldCreation);
    Assert.True(model.IsLoading);
    await model.LoadAgentPromptsAsync(Token);
    var duplicate = model.CreateAgentPromptAsync("duplicate", cancellationToken: Token);
    Assert.True(duplicate.IsCompleted);
    Assert.False(await duplicate);
    Assert.Equal(1, posts);
    Assert.False(model.HasReachedAgentPromptLimit);
    Assert.Null(model.AgentPromptCreationError);
    newPost.SetResult(Fixture().CommunityAgentPrompts[0]);
    Assert.False(await newCreation);
    Assert.False(model.IsLoading);
    service.AgentPromptCreateHandler = _ => Task.FromResult(Fixture().CommunityAgentPrompts[0]);
    Assert.True(await model.CreateAgentPromptAsync("later draft", cancellationToken: Token));
  }

  internal static CommunityAgentPromptsResponse Fixture() => JsonSerializer.Deserialize<CommunityAgentPromptsResponse>(
      ApiFixtureLoader.LoadResponse("web.communities.agent-prompts.default"), VouchaApiJson.Options)!;

  private static CommunityAgentPromptsResponse Page(CommunityAgentPromptsResponse page, string? cursor, bool more) =>
      page with { PageInfo = new(cursor, more, null), SlotInfo = new(9, 10, 1, new(3, 10)) };

  private static ScriptedCommunitiesService Service()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(true, false, membershipRole: "moderator"));
    return service;
  }

  private sealed class Session : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; private set; } = new(new User("user-1", "moderator", Roles: ["moderator"]));
    public void Change()
    {
      Current = new(new User("other", "other", Roles: ["moderator"]));
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
    }
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

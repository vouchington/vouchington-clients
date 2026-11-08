using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityAgentPromptMutationSettlementTests
{
  private static CancellationToken Token => TestContext.Current.CancellationToken;

  [Theory]
  [InlineData("cap", "before")]
  [InlineData("cancel", "before")]
  [InlineData("error", "before")]
  [InlineData("cap", "success")]
  [InlineData("cancel", "success")]
  [InlineData("error", "success")]
  [InlineData("cap", "error")]
  [InlineData("cancel", "cancel")]
  [InlineData("error", "error")]
  public async Task SupersedingReadSettlesOnlyCreationOwnedBusyState(string postFailure, string readOutcome)
  {
    var service = Service();
    using var model = new CommunityDetailViewModel(service, new Session());
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var post = new TaskCompletionSource<CommunityAgentPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
    var read = new TaskCompletionSource<CommunityAgentPromptsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptCreateHandler = _ => post.Task;
    var creating = model.CreateAgentPromptAsync("draft", cancellationToken: Token);
    service.AgentPromptPageHandler = _ => read.Task;
    var reload = model.LoadAgentPromptsAsync(Token);
    if (readOutcome == "before") { read.SetResult(Fresh()); await reload; }
    var rows = model.AgentPrompts;
    var slots = model.AgentPromptSlots;
    var hasMore = model.HasMoreCommunityList;
    Fail(post, postFailure);
    Assert.False(await creating);
    Assert.Equal(rows, model.AgentPrompts);
    Assert.Equal(slots, model.AgentPromptSlots);
    Assert.Equal(hasMore, model.HasMoreCommunityList);
    Assert.Equal(postFailure == "cap", model.HasReachedAgentPromptLimit);
    Assert.Equal(postFailure == "cap" ? "cap" : null, model.AgentPromptCreationError);
    if (readOutcome != "before")
    {
      Assert.True(model.IsLoading);
      var mutations = service.MutationCalls.Count;
      Assert.False(await model.DeleteAgentPromptAsync("prompt-1", Token));
      Assert.Equal(mutations, service.MutationCalls.Count);
      if (readOutcome == "success") { read.SetResult(Fresh()); await reload; }
      else if (readOutcome == "cancel")
      {
        read.SetCanceled(new CancellationToken(true));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reload);
      }
      else
      {
        read.SetException(new HttpRequestException("reload offline"));
        await Assert.ThrowsAsync<HttpRequestException>(() => reload);
      }
    }
    Assert.False(model.IsLoading);
    Assert.False(model.IsLoadingMoreCommunityList);
    Assert.Equal(postFailure == "cap", model.HasReachedAgentPromptLimit);
    service.AgentPromptPageHandler = _ => Task.FromResult(Fresh());
    service.AgentPromptCreateHandler = _ => Task.FromResult(Fresh().CommunityAgentPrompts[0]);
    Assert.True(await model.CreateAgentPromptAsync("retry draft", cancellationToken: Token));
  }

  [Fact]
  public async Task SelectedSectionReadFailureDoesNotAdmitSiblingWhilePostIsPending()
  {
    var service = Service();
    using var model = new CommunityDetailViewModel(service, new Session());
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var post = new TaskCompletionSource<CommunityAgentPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptCreateHandler = _ => post.Task;
    var creating = model.CreateAgentPromptAsync("draft", cancellationToken: Token);
    service.AgentPromptPageHandler = _ => Task.FromException<CommunityAgentPromptsResponse>(new HttpRequestException("reload offline"));
    await model.LoadSelectedSectionAsync(Token);
    Assert.True(model.HasError);
    Assert.False(model.IsLoading);
    var mutations = service.MutationCalls.Count;
    Assert.False(await model.DeleteAgentPromptAsync("prompt-1", Token));
    Assert.Equal(mutations, service.MutationCalls.Count);
    Fail(post, "cap");
    Assert.False(await creating);
    service.AgentPromptPageHandler = _ => Task.FromResult(Fresh());
    Assert.True(await model.DeleteAgentPromptAsync("prompt-1", Token));
  }

  [Fact]
  public async Task OldDeferredReadDoesNotReleaseNewAccountCreation()
  {
    var service = Service();
    var session = new Session();
    using var model = new CommunityDetailViewModel(service, session);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var post = new TaskCompletionSource<CommunityAgentPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
    var read = new TaskCompletionSource<CommunityAgentPromptsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptCreateHandler = _ => post.Task;
    var creating = model.CreateAgentPromptAsync("old draft", cancellationToken: Token);
    service.AgentPromptPageHandler = _ => read.Task;
    var reloading = model.LoadAgentPromptsAsync(Token);
    Fail(post, "cap");
    Assert.False(await creating);
    session.Change();
    var currentPost = new TaskCompletionSource<CommunityAgentPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptCreateHandler = _ => currentPost.Task;
    service.AgentPromptPageHandler = _ => Task.FromResult(Fresh());
    var current = model.CreateAgentPromptAsync("new account", cancellationToken: Token);
    Assert.False(current.IsCompleted);
    read.SetResult(Fresh());
    await reloading;
    Assert.True(model.IsLoading);
    Assert.False(await model.DeleteAgentPromptAsync("prompt-1", Token));
    currentPost.SetResult(Fresh().CommunityAgentPrompts[0]);
    Assert.True(await current);
    Assert.False(model.IsLoading);
  }

  [Fact]
  public async Task OldDeferredReadDoesNotReleaseDifferentSectionMutation()
  {
    var service = Service();
    using var model = new CommunityDetailViewModel(service, new Session());
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var post = new TaskCompletionSource<CommunityAgentPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
    var read = new TaskCompletionSource<CommunityAgentPromptsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptCreateHandler = _ => post.Task;
    var creating = model.CreateAgentPromptAsync("old draft", cancellationToken: Token);
    service.AgentPromptPageHandler = _ => read.Task;
    var reloading = model.LoadAgentPromptsAsync(Token);
    Fail(post, "error");
    Assert.False(await creating);
    await model.SelectSectionAsync(CommunityDetailSurfaceSection.Overview, Token);
    var update = new TaskCompletionSource<CommunityAgentPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptUpdateHandler = _ => update.Task;
    var updating = model.UpdateAgentPromptAsync("other prompt", "other action", Token);
    Assert.True(model.IsLoading);
    read.SetResult(Fresh());
    await reloading;
    Assert.True(model.IsLoading);
    update.SetResult(Fresh().CommunityAgentPrompts[0]);
    Assert.True(await updating);
    Assert.False(model.IsLoading);
  }

  private static void Fail(TaskCompletionSource<CommunityAgentPrompt> post, string failure)
  {
    if (failure == "cancel") post.SetCanceled(new CancellationToken(true));
    else post.SetException(failure == "cap"
        ? new VouchaApiException(HttpStatusCode.Conflict, "{\"code\":\"COMMUNITY_AGENT_PROMPT_LIMIT_REACHED\",\"message\":\"cap\"}")
        : new HttpRequestException("post offline"));
  }

  private static CommunityAgentPromptsResponse Fresh() => CommunityAgentPromptPagingTests.Fixture() with
  {
    CommunityAgentPrompts = [CommunityAgentPromptPagingTests.Fixture().CommunityAgentPrompts[0] with { Id = "fresh" }],
    PageInfo = new("fresh cursor", true, null),
  };

  private static ScriptedCommunitiesService Service()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(true, false, membershipRole: "moderator"));
    return service;
  }

  private sealed class Session : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; private set; } = new(new User("user-1", "user", Roles: ["moderator"]));
    public void Change()
    {
      Current = new(new User("other", "other", Roles: ["moderator"]));
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
    }
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

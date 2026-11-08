using System.Collections.Concurrent;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityAgentPromptWriteOwnershipTests
{
  private static CancellationToken Token => TestContext.Current.CancellationToken;

  [Theory]
  [InlineData("update", false, false)]
  [InlineData("delete", false, false)]
  [InlineData("allocate", false, false)]
  [InlineData("deallocate", false, false)]
  [InlineData("update", true, false)]
  [InlineData("delete", true, false)]
  [InlineData("allocate", true, false)]
  [InlineData("deallocate", true, false)]
  [InlineData("update", false, true)]
  [InlineData("delete", false, true)]
  [InlineData("allocate", false, true)]
  [InlineData("deallocate", false, true)]
  [InlineData("update", true, true)]
  [InlineData("delete", true, true)]
  [InlineData("allocate", true, true)]
  [InlineData("deallocate", true, true)]
  public async Task OldOwnerWriteCannotReloadPublishErrorOrReleaseNewOperation(string action, bool fail, bool queueReset)
  {
    var service = Service();
    var session = new Session();
    var dispatcher = new Dispatcher { Queue = queueReset };
    using var model = new CommunityDetailViewModel(service, session, uiDispatcher: dispatcher);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var old = HoldWrites(service);
    var writing = Write(model, action);
    Assert.True(model.IsLoading);
    await Task.Run(session.Change, Token);
    if (queueReset)
    {
      Finish(old, fail);
      Assert.False(await writing);
      Assert.True(model.IsLoading);
      dispatcher.Flush();
    }
    Assert.False(model.IsLoading);
    Assert.Null(model.ErrorMessage);
    Assert.Empty(model.AgentPrompts);
    Assert.Null(model.AgentPromptSlots);
    var reads = service.ForwardPageRequests.Count;
    var currentGate = HoldWrites(service);
    var current = Write(model, action);
    Assert.False(current.IsCompleted);
    if (!queueReset)
    {
      Finish(old, fail);
      Assert.False(await writing);
    }
    Assert.True(model.IsLoading);
    Assert.Null(model.ErrorMessage);
    Assert.Equal(reads, service.ForwardPageRequests.Count);
    Assert.False(await model.CreateAgentPromptAsync("must wait", cancellationToken: Token));
    currentGate.SetResult();
    Assert.True(await current);
    Assert.False(model.IsLoading);
    Assert.Equal(reads + 1, service.ForwardPageRequests.Count);
    Assert.Single(model.AgentPrompts);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task LeavingPromptPageReleasesWriteAndOldCompletionCannotAlterNewSection(bool fail)
  {
    var service = Service();
    using var model = new CommunityDetailViewModel(service, new Session());
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var gate = HoldWrites(service);
    var writing = model.DeleteAgentPromptAsync("prompt-1", Token);
    await model.SelectSectionAsync(CommunityDetailSurfaceSection.Overview, Token);
    Assert.False(model.IsLoading);
    var reads = service.ForwardPageRequests.Count;
    Finish(gate, fail);
    Assert.False(await writing);
    Assert.False(model.IsLoading);
    Assert.Null(model.ErrorMessage);
    Assert.Equal(reads, service.ForwardPageRequests.Count);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CurrentWriteFailureKeepsWorkspaceAndBlocksSiblingsUntilConcurrentReadSettles(bool canceled)
  {
    var service = Service();
    using var model = new CommunityDetailViewModel(service, new Session());
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var rows = model.AgentPrompts;
    var slots = model.AgentPromptSlots;
    var gate = HoldWrites(service);
    var writing = model.DeleteAgentPromptAsync("prompt-1", Token);
    var read = new TaskCompletionSource<CommunityAgentPromptsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptPageHandler = _ => read.Task;
    var reloading = model.LoadAgentPromptsAsync(Token);
    if (canceled) gate.SetCanceled(Token);
    else gate.SetException(new HttpRequestException("current error"));
    Assert.False(await writing);
    Assert.Equal(!canceled, model.HasError);
    Assert.Equal(rows, model.AgentPrompts);
    Assert.Equal(slots, model.AgentPromptSlots);
    var mutations = service.MutationCalls.Count;
    Assert.False(await model.AllocateAgentPromptSlotAsync("prompt-1", Token));
    Assert.False(await model.CreateAgentPromptAsync("must wait", cancellationToken: Token));
    Assert.Equal(mutations, service.MutationCalls.Count);
    read.SetResult(CommunityAgentPromptPagingTests.Fixture());
    await reloading;
    Assert.False(model.IsLoading);
    service.AgentPromptPageHandler = null;
    service.AgentPromptMutationHandler = null;
    Assert.True(await model.AllocateAgentPromptSlotAsync("prompt-1", Token));
  }

  private static TaskCompletionSource HoldWrites(ScriptedCommunitiesService service)
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptMutationHandler = _ => gate.Task;
    service.AgentPromptUpdateHandler = async _ => { await gate.Task; return CommunityAgentPromptPagingTests.Fixture().CommunityAgentPrompts[0]; };
    return gate;
  }

  private static void Finish(TaskCompletionSource gate, bool fail)
  {
    if (fail) gate.SetException(new HttpRequestException("old owner error"));
    else gate.SetResult();
  }

  private static Task<bool> Write(CommunityDetailViewModel model, string action) => action switch
  {
    "update" => model.UpdateAgentPromptAsync("prompt-1", "updated", Token),
    "delete" => model.DeleteAgentPromptAsync("prompt-1", Token),
    "allocate" => model.AllocateAgentPromptSlotAsync("prompt-1", Token),
    _ => model.DeallocateAgentPromptSlotAsync("prompt-1", Token),
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
      Current = new(new User("new-owner", "new-owner", Roles: ["moderator"]));
      SessionChanged?.Invoke(this, new(Current));
    }
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class Dispatcher : IUiThreadDispatcher
  {
    private readonly ConcurrentQueue<Action> pending = new();
    public bool Queue { get; init; }
    public bool IsDispatchRequired => Queue;
    public void Dispatch(Action action) { if (Queue) pending.Enqueue(action); else action(); }
    public void Flush() { while (pending.TryDequeue(out var action)) action(); }
  }
}

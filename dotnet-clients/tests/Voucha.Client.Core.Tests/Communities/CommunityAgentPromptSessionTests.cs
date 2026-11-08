using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityAgentPromptSessionTests
{
  private static CancellationToken Token => TestContext.Current.CancellationToken;

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CookieStoreWorkerEventResetsBoundStateOnOwningUiContext(bool injectDispatcher)
  {
    using var ui = new UiContext();
    var handler = new IdentityHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    using var cookies = new SessionCookieJar(new CookieContainer(), new Uri("https://api.test"), new Persistence());
    var store = new CookieSessionStore(new VouchaApiClient(http), cookies);
    await ((ISessionStore)store).RefreshAsync(Token);
    var eventThread = 0;
    store.SessionChanged += (_, _) => eventThread = Environment.CurrentManagedThreadId;
    CommunityDetailViewModel? model = null;
    var notifications = new List<(int Thread, SynchronizationContext? Context)>();
    if (injectDispatcher) model = new CommunityDetailViewModel(Service(), store, uiDispatcher: ui);
    ui.Invoke(() =>
    {
      model ??= new CommunityDetailViewModel(Service(), store);
      model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token).GetAwaiter().GetResult();
      model.PropertyChanged += (_, _) => notifications.Add((Environment.CurrentManagedThreadId, SynchronizationContext.Current));
    });
    try
    {
      handler.UserId = "new-owner";
      await Task.Run(() => ((IForcedSessionRefreshStore)store).RefreshAsync(true, Token), Token);
      ui.Invoke(() =>
      {
        Assert.Empty(model!.AgentPrompts);
        Assert.Empty(model.Moderation);
        Assert.Null(model.AgentPromptSlots);
        Assert.NotEmpty(notifications);
        Assert.All(notifications, item =>
        {
          Assert.Equal(ui.ThreadId, item.Thread);
          Assert.Same(ui, item.Context);
        });
      });
      Assert.NotEqual(ui.ThreadId, eventThread);
      Assert.Null(ui.PostedError);
    }
    finally { ui.Invoke(() => model!.Dispose()); }
  }

  [Fact]
  public async Task WorkerEventFencesRoundTripOwnerBeforeQueuedUiReset()
  {
    var dispatcher = new QueuedDispatcher();
    var store = new TrackingStore();
    var service = Service();
    using var model = new CommunityDetailViewModel(service, store, uiDispatcher: dispatcher);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var oldPage = new TaskCompletionSource<CommunityAgentPromptsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptPageHandler = _ => oldPage.Task;
    var reload = model.LoadAgentPromptsAsync(Token);
    await Task.Run(() => { store.Change("other"); store.Change("user-1"); }, Token);
    Assert.False(model.HasMoreCommunityList);
    Assert.False(await model.CreateAgentPromptAsync("blocked before UI reset", cancellationToken: Token));
    oldPage.SetResult(CommunityAgentPromptPagingTests.Fixture() with
    {
      CommunityAgentPrompts = [CommunityAgentPromptPagingTests.Fixture().CommunityAgentPrompts[0] with { Id = "stale" }],
    });
    await reload;
    Assert.DoesNotContain(model.AgentPrompts, item => item.Id == "stale");
    dispatcher.Flush();
    Assert.Empty(model.AgentPrompts);
    Assert.Null(model.AgentPromptSlots);
    service.AgentPromptPageHandler = _ => Task.FromResult(CommunityAgentPromptPagingTests.Fixture());
    await model.LoadAgentPromptsAsync(Token);
    Assert.Equal("prompt-1", Assert.Single(model.AgentPrompts).Id);
  }

  [Fact]
  public async Task QueuedSessionResetReleasesBusyAfterOldPostSettlesBeforeDispatch()
  {
    var dispatcher = new QueuedDispatcher();
    var store = new TrackingStore();
    var service = Service();
    using var model = new CommunityDetailViewModel(service, store, uiDispatcher: dispatcher);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    var post = new TaskCompletionSource<CommunityAgentPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentPromptCreateHandler = _ => post.Task;
    var creating = model.CreateAgentPromptAsync("old owner", cancellationToken: Token);
    await Task.Run(() => store.Change("other"), Token);
    post.SetException(new VouchaApiException(HttpStatusCode.Conflict, "{\"code\":\"COMMUNITY_AGENT_PROMPT_LIMIT_REACHED\",\"message\":\"old owner\"}"));
    Assert.False(await creating);
    Assert.Null(model.AgentPromptCreationError);
    dispatcher.Flush();
    Assert.False(model.IsLoading);
    service.AgentPromptCreateHandler = _ => Task.FromResult(CommunityAgentPromptPagingTests.Fixture().CommunityAgentPrompts[0]);
    Assert.True(await model.CreateAgentPromptAsync("new owner", cancellationToken: Token));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void SessionObserverAndQueuedWorkDoNotStronglyRetainTransientVm(bool queueBeforeCollection)
  {
    var store = new TrackingStore();
    var dispatcher = new QueuedDispatcher();
    var target = Transient(store, dispatcher, queueBeforeCollection);
    for (var attempt = 0; attempt < 3; attempt++)
    {
      GC.Collect();
      GC.WaitForPendingFinalizers();
      GC.Collect();
    }
    Assert.False(target.IsAlive);
    if (queueBeforeCollection) dispatcher.Flush(); else store.Change("other");
    Assert.Equal(0, store.Subscribers);
  }

  [Fact]
  public async Task DisposeDetachesObserverAndIgnoresAlreadyQueuedReset()
  {
    var store = new TrackingStore();
    var dispatcher = new QueuedDispatcher();
    var model = new CommunityDetailViewModel(Service(), store, uiDispatcher: dispatcher);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, Token);
    await Task.Run(() => store.Change("other"), Token);
    model.Dispose();
    var notifications = 0;
    model.PropertyChanged += (_, _) => notifications++;
    dispatcher.Flush();
    Assert.Equal(0, store.Subscribers);
    Assert.Equal(0, notifications);
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  private static WeakReference Transient(TrackingStore store, QueuedDispatcher dispatcher, bool queue)
  {
    var model = new CommunityDetailViewModel(Service(), store, uiDispatcher: dispatcher);
    if (queue) store.Change("other");
    return new WeakReference(model);
  }

  private static ScriptedCommunitiesService Service()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(true, false, membershipRole: "moderator"));
    return service;
  }

  private sealed class TrackingStore : ISessionStore
  {
    private readonly object gate = new();
    private EventHandler<SessionChangedEventArgs>? changed;
    private SessionSnapshot current = new(new User("user-1", "user", Roles: ["moderator"]));
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { lock (gate) changed += value; }
      remove { lock (gate) changed -= value; }
    }
    public int Subscribers { get { lock (gate) return changed?.GetInvocationList().Length ?? 0; } }
    public SessionSnapshot Current { get { lock (gate) return current; } }
    public void Change(string id)
    {
      EventHandler<SessionChangedEventArgs>? callback;
      SessionSnapshot snapshot;
      lock (gate)
      {
        current = snapshot = new(new User(id, id, Roles: ["moderator"]));
        callback = changed;
      }
      callback?.Invoke(this, new SessionChangedEventArgs(snapshot));
    }
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class QueuedDispatcher : IUiThreadDispatcher
  {
    private readonly ConcurrentQueue<Action> actions = new();
    public bool IsDispatchRequired => true;
    public void Dispatch(Action action) => actions.Enqueue(action);
    public void Flush() { while (actions.TryDequeue(out var action)) action(); }
  }

  private sealed class UiContext : SynchronizationContext, IUiThreadDispatcher, IDisposable
  {
    private readonly BlockingCollection<Action> work = [];
    private readonly Thread thread;
    public int ThreadId { get; private set; }
    public Exception? PostedError { get; private set; }
    public UiContext()
    {
      using var ready = new ManualResetEventSlim();
      thread = new Thread(() =>
      {
        ThreadId = Environment.CurrentManagedThreadId;
        SetSynchronizationContext(this);
        ready.Set();
        foreach (var action in work.GetConsumingEnumerable()) action();
      });
      thread.Start();
      ready.Wait();
    }
    public bool IsDispatchRequired => Environment.CurrentManagedThreadId != ThreadId;
    public void Dispatch(Action action) => Invoke(action);
    public void Invoke(Action action)
    {
      if (!IsDispatchRequired) { action(); return; }
      Exception? error = null;
      using var done = new ManualResetEventSlim();
      work.Add(() => { try { action(); } catch (Exception ex) { error = ex; } finally { done.Set(); } });
      done.Wait();
      if (error is not null) throw new InvalidOperationException("UI callback failed.", error);
    }
    public override void Post(SendOrPostCallback callback, object? state) =>
        work.Add(() => { try { callback(state); } catch (Exception ex) { PostedError = ex; } });
    public void Dispose() { work.CompleteAdding(); thread.Join(); work.Dispose(); }
  }

  private sealed class IdentityHandler : HttpMessageHandler
  {
    public string UserId { get; set; } = "user-1";
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent($"{{\"identity\":{{\"id\":\"{UserId}\",\"username\":\"member\",\"roles\":[\"moderator\"]}}}}"),
        });
  }

  private sealed class Persistence : ISessionCookiePersistence
  {
    public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    public Task WriteAsync(string serializedCookies, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

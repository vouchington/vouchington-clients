using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private readonly AgentPromptSessionObserver? agentPromptSessionObserver;
  private long agentPromptSessionRevision;
  private long appliedAgentPromptSessionRevision;

  private bool IsCurrentAgentPromptSession => AcceptsAgentPromptSession(Volatile.Read(ref agentPromptSessionRevision));

  private bool AcceptsAgentPromptSession(long revision) =>
      revision == Volatile.Read(ref agentPromptSessionRevision) &&
      revision == Volatile.Read(ref appliedAgentPromptSessionRevision);

  private void ApplyAgentPromptSessionChange(long revision)
  {
    if (agentPromptDisposed || revision != Volatile.Read(ref agentPromptSessionRevision)) return;
    OnAgentPromptSessionChanged();
    if (revision == Volatile.Read(ref agentPromptSessionRevision))
      Volatile.Write(ref appliedAgentPromptSessionRevision, revision);
  }

  private sealed class AgentPromptSessionObserver : IDisposable
  {
    private readonly WeakReference<CommunityDetailViewModel> target;
    private readonly ISessionStore store;
    private readonly IUiThreadDispatcher? dispatcher;
    private readonly SynchronizationContext? context = SynchronizationContext.Current;
    private int disposed;
    private readonly object accessGate = new();
    private NavigationViewer access;

    public AgentPromptSessionObserver(CommunityDetailViewModel model, ISessionStore store, IUiThreadDispatcher? dispatcher)
    {
      target = new(model);
      this.store = store;
      this.dispatcher = dispatcher;
      access = CaptureAccess(store.Current);
      store.SessionChanged += OnChanged;
    }

    private void OnChanged(object? sender, SessionChangedEventArgs args)
    {
      if (Volatile.Read(ref disposed) != 0) return;
      if (!target.TryGetTarget(out var model))
      {
        Dispose();
        return;
      }
      long revision;
      lock (accessGate)
      {
        var next = CaptureAccess(args.Snapshot);
        if (access.HasSameIdentityAccess(next)) return;
        access = next;
        revision = Interlocked.Increment(ref model.agentPromptSessionRevision);
      }
      if (dispatcher is not null) dispatcher.Dispatch(() => Apply(revision));
      else if (context is not null && SynchronizationContext.Current != context)
        context.Post(_ => Apply(revision), null);
      else Apply(revision);
    }

    private static NavigationViewer CaptureAccess(SessionSnapshot snapshot)
    {
      var viewer = NavigationCatalog.FromIdentity(snapshot.Identity);
      return viewer with { Roles = viewer.Roles.ToArray() };
    }

    private void Apply(long revision)
    {
      if (Volatile.Read(ref disposed) != 0) return;
      if (target.TryGetTarget(out var model)) model.ApplyAgentPromptSessionChange(revision);
      else Dispose();
    }

    public void Dispose()
    {
      if (Interlocked.Exchange(ref disposed, 1) != 0) return;
      store.SessionChanged -= OnChanged;
    }
  }
}

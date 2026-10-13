using System.Text.Json;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Agent;

public enum MemberMcpSessionStatus { Disconnected, Connecting, Connected, Error }

/// <summary>Keeps one member's MCP connection aligned with the app account session.</summary>
public sealed partial class MemberMcpSession : IDisposable
{
  private readonly ISessionStore sessionStore;
  private readonly IMemberMcpAccountTokenStore store;
  private readonly IMemberMcpSessionConnectionFactory factory;
  [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213", Justification =
      "The queued cleanup completion disposes this semaphore after in-flight operations settle.")]
  private readonly SemaphoreSlim operation = new(1, 1);
  [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213", Justification =
      "The queued cleanup completion disposes this cancellation source after in-flight operations settle.")]
  private readonly CancellationTokenSource lifetime = new();
  private readonly CancellationToken lifetimeToken;
  private readonly object gate = new();
  private Task cleanupTail;
  [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213", Justification =
      "ConnectAsync disposes its linked source in its finally block after network cancellation settles.")]
  private CancellationTokenSource? pending;
  private CancellationTokenSource? activeCancellation;
  private IMemberMcpSessionConnection? active;
  private string? accountId;
  private long generation;
  private bool disposed;

  public MemberMcpSession(ISessionStore sessionStore, IMemberMcpAccountTokenStore store,
      IMemberMcpSessionConnectionFactory factory)
  {
    ArgumentNullException.ThrowIfNull(sessionStore);
    ArgumentNullException.ThrowIfNull(store);
    ArgumentNullException.ThrowIfNull(factory);
    this.sessionStore = sessionStore;
    this.store = store;
    this.factory = factory;
    lifetimeToken = lifetime.Token;
    accountId = sessionStore.Current.Identity?.Id;
    cleanupTail = accountId is null ? store.ClearAllAccountsAsync() : Task.CompletedTask;
    sessionStore.SessionChanged += OnSessionChanged;
  }

  public event EventHandler? Changed;
  public MemberMcpSessionStatus Status { get; private set; }
  public JsonElement? Tools { get; private set; }
  public bool HasMemberSession => sessionStore.Current.Identity is not null;

  public async Task ConnectAsync(CancellationToken cancellationToken = default)
  {
    using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
    string account;
    long revision;
    CancellationToken token;
    while (true)
    {
      Task tail;
      lock (gate) { ThrowIfDisposed(); tail = cleanupTail; }
      await tail.WaitAsync(wait.Token).ConfigureAwait(false);
      await operation.WaitAsync(wait.Token).ConfigureAwait(false);
      try
      {
        lock (gate)
        {
          if (!ReferenceEquals(tail, cleanupTail))
          {
            operation.Release();
            continue;
          }
          ThrowIfDisposed();
          account = accountId ?? throw new InvalidOperationException("A member session is required.");
          if (sessionStore.Current.Identity?.Id != account)
            throw new OperationCanceledException("The member account changed.");
          if (active is not null)
          {
            operation.Release();
            return;
          }
          pending = CancellationTokenSource.CreateLinkedTokenSource(wait.Token);
          token = pending.Token;
          revision = generation;
          Status = MemberMcpSessionStatus.Connecting;
        }
      }
      catch { operation.Release(); throw; }
      break;
    }
    IMemberMcpSessionConnection? created = null;
    try
    {
      Changed?.Invoke(this, EventArgs.Empty);
      created = await factory.CreateAsync(account, token).ConfigureAwait(false);
      var tools = await created.ConnectAndListToolsAsync(token).ConfigureAwait(false);
      token.ThrowIfCancellationRequested();
      lock (gate)
      {
        if (generation != revision || accountId != account || sessionStore.Current.Identity?.Id != account)
          throw new OperationCanceledException("The member account changed.");
        active = created;
        activeCancellation = new CancellationTokenSource();
        created = null;
        Tools = tools;
        Status = MemberMcpSessionStatus.Connected;
      }
      Changed?.Invoke(this, EventArgs.Empty);
    }
    catch (Exception error)
    {
      if (created is not null)
      {
        try
        {
          if (error is OperationCanceledException || sessionStore.Current.Identity?.Id != accountId)
            await created.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally { await created.DisposeAsync().ConfigureAwait(false); }
      }
      lock (gate)
      {
        if (Status == MemberMcpSessionStatus.Connecting)
          Status = error is OperationCanceledException ? MemberMcpSessionStatus.Disconnected :
              MemberMcpSessionStatus.Error;
      }
      Changed?.Invoke(this, EventArgs.Empty);
      throw;
    }
    finally
    {
      lock (gate) { pending?.Dispose(); pending = null; }
      operation.Release();
    }
  }

  public async Task RefreshToolsAsync(CancellationToken cancellationToken = default)
  {
    using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
    await operation.WaitAsync(wait.Token).ConfigureAwait(false);
    try
    {
      IMemberMcpSessionConnection connection;
      long revision;
      CancellationToken sessionToken;
      lock (gate)
      {
        ThrowIfDisposed();
        connection = active ?? throw new InvalidOperationException("MCP is not connected.");
        Tools = null;
        revision = ++generation;
        sessionToken = activeCancellation?.Token ?? throw new InvalidOperationException("MCP is not connected.");
      }
      Changed?.Invoke(this, EventArgs.Empty);
      using var linked = CancellationTokenSource.CreateLinkedTokenSource(wait.Token, sessionToken);
      JsonElement tools;
      try { tools = await connection.ListToolsAsync(linked.Token).ConfigureAwait(false); }
      catch
      {
        var invalidated = false;
        lock (gate)
        {
          if (generation == revision && ReferenceEquals(active, connection))
          {
            generation++;
            invalidated = true;
          }
        }
        if (invalidated) Changed?.Invoke(this, EventArgs.Empty);
        throw;
      }
      lock (gate)
      {
        if (generation != revision || !ReferenceEquals(active, connection)) return;
        Tools = tools;
      }
      Changed?.Invoke(this, EventArgs.Empty);
    }
    finally { operation.Release(); }
  }

  public Task DisconnectAsync()
  {
    lock (gate)
    {
      ThrowIfDisposed();
      return QueueCleanup(accountId);
    }
  }

  public Task WhenSettledAsync()
  {
    lock (gate) return cleanupTail;
  }

}

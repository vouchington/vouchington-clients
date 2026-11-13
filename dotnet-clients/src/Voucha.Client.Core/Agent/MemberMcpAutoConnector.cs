using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Agent;

/// <summary>
/// Connects member MCP as soon as a native member session exists, with no user action. A failed
/// connect leaves the session in its error state; it is retried on the next account change or
/// explicit connect, never in a loop.
/// </summary>
public sealed class MemberMcpAutoConnector : IDisposable
{
  private readonly ISessionStore sessionStore;
  private readonly MemberMcpSession session;
  private bool disposed;

  public MemberMcpAutoConnector(ISessionStore sessionStore, MemberMcpSession session)
  {
    ArgumentNullException.ThrowIfNull(sessionStore);
    ArgumentNullException.ThrowIfNull(session);
    this.sessionStore = sessionStore;
    this.session = session;
    sessionStore.SessionChanged += OnSessionChanged;
  }

  /// <summary>Connects for the current member, used after confirmed startup restoration.</summary>
  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification =
      "Automatic connect is best effort; the session reports failure through its status.")]
  public async Task ConnectIfSignedInAsync()
  {
    if (disposed || sessionStore.Current.Identity is null) return;
    try { await session.ConnectAsync().ConfigureAwait(false); }
    catch (Exception error) when (error is not OutOfMemoryException)
    {
      System.Diagnostics.Debug.WriteLine($"Automatic MCP connect failed: {error.GetType().Name}");
    }
  }

  private void OnSessionChanged(object? sender, SessionChangedEventArgs args)
  {
    if (args.Snapshot.Identity is not null) _ = ConnectIfSignedInAsync();
  }

  public void Dispose()
  {
    disposed = true;
    sessionStore.SessionChanged -= OnSessionChanged;
  }
}

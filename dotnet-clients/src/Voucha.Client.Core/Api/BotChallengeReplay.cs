using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Api;

/// <summary>Creates an account-bound, single-request Turnstile proof lifetime.</summary>
public sealed class BotChallengeReplay(
    Func<ISessionStore> sessionStore,
    Func<string, CancellationToken, Task<string>> getToken)
{
  public const string Action = "bot-challenge.mutation";
  public const string TokenHeader = "x-voucha-bot-challenge-token";

  internal Scope Begin(CancellationToken cancellationToken) =>
      new(sessionStore(), getToken, cancellationToken);

  internal sealed class Scope : IDisposable
  {
    private readonly ISessionStore store;
    private readonly Func<string, CancellationToken, Task<string>> getToken;
    private readonly CancellationTokenSource cancellation;
    private readonly string? account;
    private readonly object gate = new();
    private bool disposed;

    internal Scope(ISessionStore store, Func<string, CancellationToken, Task<string>> getToken,
        CancellationToken cancellationToken)
    {
      this.store = store;
      this.getToken = getToken;
      cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      account = store.Current.Identity?.Id;
      store.SessionChanged += AccountChanged;
      if (store.Current.Identity?.Id != account) cancellation.Cancel();
    }

    internal CancellationToken Token => cancellation.Token;

    internal async Task<string> SolveAsync()
    {
      Token.ThrowIfCancellationRequested();
      var token = await getToken(Action, Token).WaitAsync(Token).ConfigureAwait(false);
      Token.ThrowIfCancellationRequested();
      if (store.Current.Identity?.Id != account) throw new OperationCanceledException(Token);
      return token;
    }

    private void AccountChanged(object? sender, SessionChangedEventArgs args)
    {
      lock (gate)
      {
        if (!disposed && args.Snapshot.Identity?.Id != account) cancellation.Cancel();
      }
    }

    public void Dispose()
    {
      store.SessionChanged -= AccountChanged;
      lock (gate)
      {
        disposed = true;
        cancellation.Dispose();
      }
    }
  }
}

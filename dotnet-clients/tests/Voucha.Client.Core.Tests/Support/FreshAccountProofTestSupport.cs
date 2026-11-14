using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Tests.Support;

internal sealed class ConfirmationSessionStore : ISessionStore
{
  public event EventHandler<SessionChangedEventArgs>? SessionChanged;
  public SessionSnapshot Current { get; private set; } = new(new User("account-a", "alice"));
  public int RefreshCalls { get; private set; }
  public void Change(string? id)
  {
    Current = new(id is null ? null : new User(id, "next"));
    SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
  }
  public Task RefreshAsync(CancellationToken cancellationToken = default) { RefreshCalls++; return Task.CompletedTask; }
  public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FreshAccountProofServiceStub : IFreshAccountProofService
{
  public ReAuthMethodsResponse Methods { get; init; } = new(true, true, true, ["github", "google"]);
  public int EmailRequests { get; private set; }
  public List<(bool Email, string Code)> Codes { get; } = [];
  public List<string> Providers { get; } = [];
  public List<string> PasskeyIds { get; } = [];
  public List<string?> Afters { get; } = [];
  public Func<Task<string>> Verification { get; set; } = () => Task.FromResult("proof-token");
  public Func<string?, ConfirmationPasskeyPage> Page { get; set; } = _ => new([new("owned-passkey", "Device")], new(null, false, null));
  public Task<ReAuthMethodsResponse> DiscoverAsync(CancellationToken cancellationToken) => Task.FromResult(Methods);
  public Task RequestEmailAsync(CancellationToken cancellationToken) { EmailRequests++; return Task.CompletedTask; }
  public Task<string> VerifyCodeAsync(bool email, string code, CancellationToken cancellationToken)
  { Codes.Add((email, code)); return Verification(); }
  public Task<ConfirmationPasskeyPage> FetchPasskeysAsync(string? after, CancellationToken cancellationToken)
  { Afters.Add(after); return Task.FromResult(Page(after)); }
  public Task<string> VerifyPasskeyAsync(string id, CancellationToken cancellationToken)
  { PasskeyIds.Add(id); return Verification(); }
  public Task<string> VerifyProviderAsync(string provider, CancellationToken cancellationToken)
  { Providers.Add(provider); return Verification(); }
}

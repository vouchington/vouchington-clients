using Voucha.Client.Core.Api;
using Voucha.Client.Core.Fediverse;

namespace Voucha.Client.Core.Auth;

public sealed class FreshAccountBroker(VouchaApiClient client, INativeExternalBrowser browser)
{
  private readonly object gate = new();
  private Pending? pending;

  public async Task<string> VerifyAsync(string provider, CancellationToken cancellationToken)
  {
    if (provider is not ("facebook" or "x" or "github" or "linkedin" or "microsoft"))
      throw new NotSupportedException();
    var proof = NativeAuthorizationCompletionProof.Create();
    lock (gate)
      if (pending is not null) throw new InvalidOperationException();
    var authorization = await client.SendAsync<BeginOAuthAuthorizationResponse>(
        VouchaApiEndpoints.BeginReAuthProviderAuthorization(provider, proof.Challenge), cancellationToken)
        .ConfigureAwait(true);
    if (string.IsNullOrWhiteSpace(authorization.FlowId) || authorization.RedirectUrl.Scheme != Uri.UriSchemeHttps ||
        authorization.ExpiresAt <= DateTimeOffset.UtcNow)
      throw new InvalidOperationException();
    var flow = new Pending(provider, authorization, proof.Verifier,
        new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously));
    var remaining = authorization.ExpiresAt - DateTimeOffset.UtcNow;
    if (remaining <= TimeSpan.Zero || remaining.TotalMilliseconds > uint.MaxValue - 1) throw new InvalidOperationException();
    using var expiration = new CancellationTokenSource(remaining);
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, expiration.Token);
    flow.Cancellation = linked.Token;
    lock (gate)
    {
      if (pending is not null) throw new InvalidOperationException();
      pending = flow;
    }
    try
    {
      if (!await browser.OpenAsync(authorization.RedirectUrl, linked.Token).ConfigureAwait(false))
        throw new OperationCanceledException();
      return await flow.Completion.Task.WaitAsync(linked.Token).ConfigureAwait(false);
    }
    finally
    {
      lock (gate)
        if (ReferenceEquals(pending, flow)) pending = null;
    }
  }

  public async Task<bool> HandleCallbackAsync(Uri uri, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(uri);
    if (uri.Scheme != "voucha" || uri.Host != "auth" || uri.AbsolutePath != "/oauth/callback") return false;
    var query = NativeCallbackQuery.Parse(uri.Query);
    Pending? flow;
    lock (gate)
    {
      flow = pending;
      if (flow is null || !query.TryGetValue("flow_id", out var id) || id != flow.Authorization.FlowId) return false;
      if (flow.Claimed || !query.TryGetValue("completion_token", out var token) || string.IsNullOrWhiteSpace(token))
        return true;
      flow.Claimed = true;
    }
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, flow.Cancellation);
    try
    {
      while (DateTimeOffset.UtcNow < flow.Authorization.ExpiresAt)
      {
        lock (gate)
          if (!ReferenceEquals(pending, flow)) return true;
        var completion = await client.CompleteOAuthAuthorizationAsync(flow.Authorization.FlowId,
            query["completion_token"], flow.Verifier, linked.Token).ConfigureAwait(false);
        if (completion.Kind == OAuthCompletionKind.Pending)
        {
          await Task.Delay(TimeSpan.FromSeconds(1), linked.Token).ConfigureAwait(false);
          continue;
        }
        linked.Token.ThrowIfCancellationRequested();
        lock (gate)
          if (!ReferenceEquals(pending, flow)) return true;
        if (completion.Kind != OAuthCompletionKind.Connected) throw new InvalidOperationException();
        var proof = await client.SendAsync<ReAuthTokenResponse>(VouchaApiEndpoints.ProveReAuthProvider(
            flow.Provider, flow.Authorization.FlowId), linked.Token).ConfigureAwait(false);
        flow.Completion.TrySetResult(proof.ReAuthToken);
        return true;
      }
      flow.Completion.TrySetCanceled(cancellationToken);
    }
    catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or OperationCanceledException)
    {
      flow.Completion.TrySetException(exception);
    }
    return true;
  }

  private sealed record Pending(string Provider, BeginOAuthAuthorizationResponse Authorization,
      string Verifier, TaskCompletionSource<string> Completion)
  {
    public bool Claimed { get; set; }
    public CancellationToken Cancellation { get; set; }
  }
}

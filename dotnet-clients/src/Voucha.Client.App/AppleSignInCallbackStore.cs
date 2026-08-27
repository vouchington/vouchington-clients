using System.Net;
using System.Security.Cryptography;
using Voucha.Client.Core;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

public sealed class AppleSignInCallbackStore
{
  private static readonly TimeSpan BrowserSignInTimeout = TimeSpan.FromMinutes(5);
  private readonly AppConfig config;
  private readonly object gate = new();
  private PendingAppleSignIn? pending;

  public AppleSignInCallbackStore(AppConfig config) =>
      this.config = config;

  public async Task<AppleSignInCredential> StartAsync(CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(config.AppleClientId))
    {
      throw new InvalidOperationException("Apple sign-in is not configured.");
    }

    var state = NewUrlToken();
    var rawNonce = NewUrlToken();
    var completion = new TaskCompletionSource<AppleSignInCredential>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    lock (gate)
    {
      pending?.Completion.TrySetCanceled(CancellationToken.None);
      pending = new PendingAppleSignIn(state, rawNonce, completion);
    }

    try
    {
      if (!await Launcher.Default.OpenAsync(AppleAuthorizationUrl.Create(config, state, rawNonce)).ConfigureAwait(true))
      {
        throw new InvalidOperationException("Apple sign-in could not open the browser.");
      }

      using var timeout = new CancellationTokenSource(BrowserSignInTimeout);
      using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
          cancellationToken,
          timeout.Token);
      using var registration = linkedCancellation.Token.Register(
          () => Cancel(state, linkedCancellation.Token));
      return await completion.Task.WaitAsync(linkedCancellation.Token).ConfigureAwait(false);
    }
    finally
    {
      Clear(state);
    }
  }

  public bool TryComplete(Uri uri)
  {
    if (!IsAppleCallback(uri)) return false;
    var values = ParseQuery(uri.Query);
    if (!values.TryGetValue("state", out var state) || string.IsNullOrWhiteSpace(state)) return true;

    PendingAppleSignIn? current;
    lock (gate) current = pending;
    if (current is null || !string.Equals(current.State, state, StringComparison.Ordinal))
    {
      return true;
    }

    if (values.TryGetValue("error", out var error) && !string.IsNullOrWhiteSpace(error))
    {
      current.Completion.TrySetException(new InvalidOperationException(error));
      return true;
    }

    if (!values.TryGetValue("id_token", out var token) || string.IsNullOrWhiteSpace(token))
    {
      current.Completion.TrySetException(new InvalidOperationException("Apple sign-in did not return an identity token."));
      return true;
    }

    var name = TryGetName(values);
    current.Completion.TrySetResult(new AppleSignInCredential(token, current.Nonce, string.IsNullOrWhiteSpace(name) ? null : name));
    return true;
  }

  private static bool IsAppleCallback(Uri uri) =>
      uri.Scheme == "voucha" &&
      string.Equals(uri.Host, "auth", StringComparison.OrdinalIgnoreCase) &&
      string.Equals(uri.AbsolutePath, "/apple/callback", StringComparison.OrdinalIgnoreCase);

  private static Dictionary<string, string> ParseQuery(string query)
  {
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
      var parts = pair.Split('=', 2);
      result[WebUtility.UrlDecode(parts[0])] = parts.Length == 2 ? WebUtility.UrlDecode(parts[1]) : "";
    }
    return result;
  }

  private static string? TryGetName(Dictionary<string, string> values)
  {
    foreach (var key in new[] { "userName", "user_name", "name" })
    {
      if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
      {
        return value;
      }
    }

    return null;
  }

  private static string NewUrlToken() =>
      Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

  private void Cancel(string state, CancellationToken cancellationToken)
  {
    PendingAppleSignIn? current;
    lock (gate) current = pending;
    if (current is not null && string.Equals(current.State, state, StringComparison.Ordinal))
    {
      current.Completion.TrySetCanceled(cancellationToken);
    }
  }

  private void Clear(string state)
  {
    lock (gate)
    {
      if (pending is not null && string.Equals(pending.State, state, StringComparison.Ordinal))
      {
        pending = null;
      }
    }
  }

  private sealed record PendingAppleSignIn(
      string State,
      string Nonce,
      TaskCompletionSource<AppleSignInCredential> Completion);
}

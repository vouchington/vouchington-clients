using System.Text.Json.Serialization;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

public sealed record SessionSnapshot(User? Identity)
{
  public bool IsAuthenticated => Identity is not null;

  public static SessionSnapshot Anonymous { get; } = new(default(User?));
}

public sealed class SessionChangedEventArgs : EventArgs
{
  public SessionChangedEventArgs(SessionSnapshot snapshot) => Snapshot = snapshot;

  public SessionSnapshot Snapshot { get; }
}

public interface ISessionStore
{
  event EventHandler<SessionChangedEventArgs>? SessionChanged;

  SessionSnapshot Current { get; }

  Task RefreshAsync(CancellationToken cancellationToken = default);

  Task SignOutAsync(CancellationToken cancellationToken = default);
}

public interface IForcedSessionRefreshStore
{
  Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default);
}

public interface IConfirmingSessionRefreshStore
{
  Task RefreshConfirmingAsync(CancellationToken cancellationToken = default);
}

public interface ICommittedOAuthDisconnectSessionStore
{
  void ApplyCommittedOAuthDisconnect(OAuthBrokerProvider provider);
}

#if DEBUG
public interface IDevelopmentSessionStore
{
  Task InjectDevelopmentCookiesAsync(
      string deviceToken,
      string sessionToken,
      CancellationToken cancellationToken = default);
}
#endif

public sealed record MfaChallenge(string LoginAttemptId);

public sealed record PasskeyAuthenticationOptions(
    string Challenge,
    string RpId,
    string? UserVerification,
    IReadOnlyList<PasskeyCredentialDescriptor>? AllowCredentials);

public sealed record AppleSignInCredential(string Token, string? Nonce, string? UserName);

public sealed record PasskeyCredentialDescriptor(string Id);

public sealed record PasskeyAssertionResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("rawId")] string RawId,
    [property: JsonPropertyName("response")] PasskeyAssertionAuthenticatorResponse Response)
{
  [JsonPropertyName("type")]
  public string Type { get; init; } = "public-key";

  [JsonPropertyName("clientExtensionResults")]
  public IReadOnlyDictionary<string, object> ClientExtensionResults { get; } =
      new Dictionary<string, object>(StringComparer.Ordinal);
}

public sealed record PasskeyAssertionAuthenticatorResponse(
    [property: JsonPropertyName("authenticatorData")] string AuthenticatorData,
    [property: JsonPropertyName("clientDataJSON")] string ClientDataJSON,
    [property: JsonPropertyName("signature")] string Signature,
    [property: JsonPropertyName("userHandle")] string UserHandle);

public interface IPasskeyAssertionProvider
{
  Task<PasskeyAssertionResponse> GetAssertionAsync(
      PasskeyAuthenticationOptions options,
      CancellationToken cancellationToken = default);
}

public interface IAppleSignInProvider
{
  Task<AppleSignInCredential> GetCredentialAsync(CancellationToken cancellationToken = default);
}

using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

public sealed class AuthenticationService
{
  private readonly VouchaApiClient client;
  private readonly ISessionStore sessionStore;
  private readonly IPasskeyAssertionProvider passkeyAssertionProvider;
  private readonly IAppleSignInProvider? appleSignInProvider;
  private readonly ISupersededNativeOAuthAuthorizationDiscarder? oauthAuthorizationDiscarder;

  public AuthenticationService(
      VouchaApiClient client,
      ISessionStore sessionStore,
      IPasskeyAssertionProvider passkeyAssertionProvider)
      : this(client, sessionStore, passkeyAssertionProvider, null, null)
  {
  }

  public AuthenticationService(
      VouchaApiClient client,
      ISessionStore sessionStore,
      IPasskeyAssertionProvider passkeyAssertionProvider,
      IAppleSignInProvider? appleSignInProvider)
      : this(client, sessionStore, passkeyAssertionProvider, appleSignInProvider, null)
  {
  }

  public AuthenticationService(
      VouchaApiClient client,
      ISessionStore sessionStore,
      IPasskeyAssertionProvider passkeyAssertionProvider,
      IAppleSignInProvider? appleSignInProvider,
      ISupersededNativeOAuthAuthorizationDiscarder? oauthAuthorizationDiscarder)
  {
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    this.passkeyAssertionProvider = passkeyAssertionProvider ??
        throw new ArgumentNullException(nameof(passkeyAssertionProvider));
    this.appleSignInProvider = appleSignInProvider;
    this.oauthAuthorizationDiscarder = oauthAuthorizationDiscarder;
  }

  public Task RequestEmailOtpAsync(
      string email,
      string turnstileToken,
      CancellationToken cancellationToken = default) =>
      RequestEmailOtpAsync(email, turnstileToken, null, cancellationToken);

  public Task RequestEmailOtpAsync(
      string email,
      string turnstileToken,
      string? uiLocale,
      CancellationToken cancellationToken = default) =>
      client.SendAsync(
          VouchaApiEndpoints.RequestEmailOtp(email, turnstileToken, uiLocale),
          cancellationToken);

  public async Task<MfaChallenge?> VerifyEmailOtpAsync(
      string email,
      string code,
      CancellationToken cancellationToken = default)
  {
    var response = await client
        .SendAsync<AuthResponse>(
            VouchaApiEndpoints.VerifyEmailOtp(email, code),
            cancellationToken)
        .ConfigureAwait(false);
    return await CompleteAuthAsync(response, cancellationToken).ConfigureAwait(false);
  }

  public async Task<MfaChallenge?> SignInWithPasskeyAsync(
      CancellationToken cancellationToken = default)
  {
    var optionsResponse = await client
        .SendAsync<PasskeyAuthenticationOptionsResponse>(
            VouchaApiEndpoints.PasskeyAuthOptions(),
            cancellationToken)
        .ConfigureAwait(false);
    if (optionsResponse?.Options is null)
    {
      throw new InvalidOperationException("Failed to retrieve passkey authentication options from the server.");
    }

    var assertion = await passkeyAssertionProvider
        .GetAssertionAsync(optionsResponse.Options, cancellationToken)
        .ConfigureAwait(false);
    var response = await client
        .SendAsync<AuthResponse>(
            VouchaApiEndpoints.PasskeyAuthVerify(assertion),
            cancellationToken)
        .ConfigureAwait(false);
    return await CompleteAuthAsync(response, cancellationToken).ConfigureAwait(false);
  }

  public async Task<MfaChallenge?> SignInWithAppleAsync(
      CancellationToken cancellationToken = default)
  {
    var provider = appleSignInProvider ??
        throw new InvalidOperationException("Apple sign-in is not configured.");

    var credential = await provider.GetCredentialAsync(cancellationToken).ConfigureAwait(false);
    ArgumentNullException.ThrowIfNull(credential);

    var response = await client
        .SendAsync<AuthResponse>(
            VouchaApiEndpoints.AppleSignIn(credential.Token, credential.Nonce, credential.UserName),
            cancellationToken)
        .ConfigureAwait(false);
    return await CompleteAuthAsync(response, cancellationToken).ConfigureAwait(false);
  }

  public async Task VerifyMfaTotpAsync(
      string loginAttemptId,
      string code,
      CancellationToken cancellationToken = default)
  {
    await client
        .SendAsync(
            VouchaApiEndpoints.VerifyMfaTotp(loginAttemptId, code),
            cancellationToken)
        .ConfigureAwait(false);
    await CompleteSuccessfulAuthenticationAsync(
        "MFA verification did not create a session.",
        cancellationToken).ConfigureAwait(false);
  }

  private async Task<MfaChallenge?> CompleteAuthAsync(
      AuthResponse response,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(response);

    if (response.MfaRequired == true)
    {
      return string.IsNullOrWhiteSpace(response.LoginAttemptId)
          ? throw new InvalidOperationException("MFA was required without a login attempt id.")
          : new MfaChallenge(response.LoginAttemptId);
    }

    await CompleteSuccessfulAuthenticationAsync(
        "Authentication did not create a session.",
        cancellationToken).ConfigureAwait(false);

    return null;
  }

  private Task RefreshAfterAuthAsync(CancellationToken cancellationToken) =>
      sessionStore is IForcedSessionRefreshStore forcedRefreshStore
          ? forcedRefreshStore.RefreshAsync(force: true, cancellationToken)
          : sessionStore.RefreshAsync(cancellationToken);

  private async Task CompleteSuccessfulAuthenticationAsync(
      string missingSessionMessage,
      CancellationToken cancellationToken)
  {
    await RefreshAfterAuthAsync(cancellationToken).ConfigureAwait(false);
    if (!sessionStore.Current.IsAuthenticated)
    {
      throw new UnauthorizedAccessException(missingSessionMessage);
    }
    if (oauthAuthorizationDiscarder is not null)
    {
      await oauthAuthorizationDiscarder
          .DiscardAfterSuccessfulAuthenticationAsync()
          .ConfigureAwait(false);
    }
  }

}

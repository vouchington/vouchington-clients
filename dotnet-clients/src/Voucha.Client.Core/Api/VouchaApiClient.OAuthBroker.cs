namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<OAuthProvidersResponse> FetchOAuthProvidersAsync(
      CancellationToken cancellationToken = default) =>
      SendAsync<OAuthProvidersResponse>(VouchaApiEndpoints.OAuthProviders(), cancellationToken);

  public Task<BeginOAuthAuthorizationResponse> BeginOAuthAuthorizationAsync(
      OAuthBrokerProvider provider,
      OAuthAuthorizationPurpose purpose,
      string completionProofChallenge,
      CancellationToken cancellationToken = default) =>
      SendAsync<BeginOAuthAuthorizationResponse>(
          VouchaApiEndpoints.BeginOAuthAuthorization(provider, purpose, completionProofChallenge),
          cancellationToken);

  public Task<OAuthCompletionResponse> CompleteOAuthAuthorizationAsync(
      string flowId,
      string completionToken,
      string completionProofVerifier,
      CancellationToken cancellationToken = default) =>
      SendAsync<OAuthCompletionResponse>(
          VouchaApiEndpoints.CompleteOAuthAuthorization(
              flowId,
              completionToken,
              completionProofVerifier),
          cancellationToken);

  public Task DisconnectOAuthAccountAsync(
      OAuthBrokerProvider provider,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DisconnectOAuthAccount(provider), cancellationToken);
}

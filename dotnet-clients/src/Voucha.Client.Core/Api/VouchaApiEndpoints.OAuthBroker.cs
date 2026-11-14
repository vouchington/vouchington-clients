namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest OAuthProviders() =>
      Get("/api/v1/auth/oauth/providers");

  public static ApiRequest BeginOAuthAuthorization(
      OAuthBrokerProvider provider,
      OAuthAuthorizationPurpose purpose,
      string completionProofChallenge, string? reAuthToken = null) =>
      new ApiRequest(
          HttpMethod.Post,
          $"/api/v1/auth/oauth/{Path(OAuthProviderValue(provider))}/authorizations")
      {
        Body = new BeginOAuthAuthorizationBody(purpose, "native", completionProofChallenge, reAuthToken),
      }.ConfirmWith(reAuthToken, proof => BeginOAuthAuthorization(provider, purpose, completionProofChallenge, proof),
          required: purpose == OAuthAuthorizationPurpose.Connect);

  public static ApiRequest CompleteOAuthAuthorization(
      string flowId,
      string completionToken,
      string completionProofVerifier) =>
      new(HttpMethod.Post, $"/api/v1/auth/oauth/authorizations/{Path(flowId)}/complete")
      {
        Body = new CompleteOAuthAuthorizationBody(completionToken, completionProofVerifier),
      };

  public static ApiRequest DisconnectOAuthAccount(OAuthBrokerProvider provider, string? reAuthToken = null) =>
      new ApiRequest(
          HttpMethod.Delete,
          $"/api/v1/auth/oauth/{Path(OAuthProviderValue(provider))}/connect")
      { Body = reAuthToken is null ? null : new ReAuthTokenBody(reAuthToken) }
      .ConfirmWith(reAuthToken, proof => DisconnectOAuthAccount(provider, proof));

  private static string OAuthProviderValue(OAuthBrokerProvider provider) => provider switch
  {
    OAuthBrokerProvider.Facebook => "facebook",
    OAuthBrokerProvider.X => "x",
    OAuthBrokerProvider.Github => "github",
    OAuthBrokerProvider.Linkedin => "linkedin",
    OAuthBrokerProvider.Microsoft => "microsoft",
    OAuthBrokerProvider.Google => "google",
    _ => throw new ArgumentOutOfRangeException(nameof(provider)),
  };
}

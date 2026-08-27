namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest OAuthProviders() =>
      Get("/api/v1/auth/oauth/providers");

  public static ApiRequest BeginOAuthAuthorization(
      OAuthBrokerProvider provider,
      OAuthAuthorizationPurpose purpose,
      string completionProofChallenge) =>
      new(
          HttpMethod.Post,
          $"/api/v1/auth/oauth/{Path(OAuthProviderValue(provider))}/authorizations")
      {
        Body = new BeginOAuthAuthorizationBody(purpose, "native", completionProofChallenge),
      };

  public static ApiRequest CompleteOAuthAuthorization(
      string flowId,
      string completionToken,
      string completionProofVerifier) =>
      new(HttpMethod.Post, $"/api/v1/auth/oauth/authorizations/{Path(flowId)}/complete")
      {
        Body = new CompleteOAuthAuthorizationBody(completionToken, completionProofVerifier),
      };

  public static ApiRequest DisconnectOAuthAccount(OAuthBrokerProvider provider) =>
      new(
          HttpMethod.Delete,
          $"/api/v1/auth/oauth/{Path(OAuthProviderValue(provider))}/connect");

  private static string OAuthProviderValue(OAuthBrokerProvider provider) => provider switch
  {
    OAuthBrokerProvider.Facebook => "facebook",
    OAuthBrokerProvider.X => "x",
    OAuthBrokerProvider.Github => "github",
    _ => throw new ArgumentOutOfRangeException(nameof(provider)),
  };
}

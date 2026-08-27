using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class ApiFixtureEndpointCoverageTests
{
  private static IReadOnlyDictionary<string, ApiRequest> WithOAuthBrokerEndpoints(
      IReadOnlyDictionary<string, ApiRequest> existing)
  {
    var registry = new Dictionary<string, ApiRequest>(existing, StringComparer.Ordinal)
    {
      ["native.oauth.providers.broker-capabilities"] = VouchaApiEndpoints.OAuthProviders(),
      ["native.oauth.authorization.begin"] = VouchaApiEndpoints.BeginOAuthAuthorization(
          OAuthBrokerProvider.Github,
          OAuthAuthorizationPurpose.Authenticate,
          "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ"),
      ["native.oauth.authorization.complete.pending"] = CompleteAuthorization(),
      ["native.oauth.authorization.complete.authenticated"] = CompleteAuthorization(),
      ["native.oauth.authorization.complete.mfa"] = CompleteAuthorization(),
      ["native.oauth.authorization.complete.connected"] = CompleteAuthorization(),
      ["native.friend-recommendations.default"] = VouchaApiEndpoints.FriendRecommendations(),
      ["native.friend-recommendations.second-page"] =
          VouchaApiEndpoints.FriendRecommendations(FirstRecommendationCursor),
      ["native.friend-recommendations.empty"] =
          VouchaApiEndpoints.FriendRecommendations(SecondRecommendationCursor),
    };
    return registry;
  }

  private static ApiRequest CompleteAuthorization() =>
      VouchaApiEndpoints.CompleteOAuthAuthorization(
          "019fafb8-a44c-73e2-890a-497ff3dd27a6",
          "native-completion-token",
          "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV");

  private const string FirstRecommendationCursor =
      "eyJpZCI6InVzZXItMSIsInNjb3BlIjoie1wicmVzb3VyY2VcIjpcIm15LWZyaWVuZC1yZWNvbW1lbmRhdGlvbnNcIixcIm93bmVyX2lkXCI6XCJmaXh0dXJlLXVzZXJcIixcIm9yZGVyXCI6XCJpZC1hc2NcIn0ifQ";
  private const string SecondRecommendationCursor =
      "eyJpZCI6IjAxOWZhZmMxLTMwOGQtN2RiNS1iODM0LTZiYmY2NDFiZjBlNyIsInNjb3BlIjoie1wicmVzb3VyY2VcIjpcIm15LWZyaWVuZC1yZWNvbW1lbmRhdGlvbnNcIixcIm93bmVyX2lkXCI6XCJmaXh0dXJlLXVzZXJcIixcIm9yZGVyXCI6XCJpZC1hc2NcIn0ifQ";
}

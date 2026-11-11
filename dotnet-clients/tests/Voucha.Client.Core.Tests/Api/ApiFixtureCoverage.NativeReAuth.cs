using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  private const string ReAuthFlowId = "00000000-0000-7000-8000-000000000742";
  private const string ReAuthPasskeyId = "00000000-0000-7000-8000-000000000741";

  public static Dictionary<string, Type> WithNativeReAuthFixtures(this Dictionary<string, Type> registry)
  {
    registry["native.auth.re-auth.methods.default"] = typeof(ReAuthMethodsResponse);
    registry["native.auth.re-auth.email.tokens"] = typeof(NativeEmailTokenResponse);
    foreach (var id in new[]
    {
      "native.auth.re-auth.email.verification",
      "native.auth.re-auth.totp.verification",
      "native.auth.re-auth.passkeys.verification",
      "native.auth.re-auth.providers.verification.google",
      "native.auth.re-auth.providers.proof.github",
    })
    {
      registry[id] = typeof(ReAuthTokenResponse);
    }

    registry["native.auth.re-auth.passkeys.options"] = typeof(ReAuthPasskeyOptionsResponse);
    registry["native.auth.re-auth.providers.challenge.google"] = typeof(ReAuthProviderChallengeResponse);
    registry["native.auth.re-auth.providers.authorization.github"] = typeof(BeginOAuthAuthorizationResponse);
    registry["native.auth.session.refresh.rotated"] = typeof(NativeSessionRefreshResponse);
    registry["native.bot-challenge.required"] = typeof(BotChallengeRequiredResponse);
    return registry;
  }

  private static Dictionary<string, ApiRequest> WithNativeReAuthEndpoints(
      this Dictionary<string, ApiRequest> registry)
  {
    registry["native.auth.re-auth.methods.default"] = VouchaApiEndpoints.ReAuthMethods();
    registry["native.auth.re-auth.email.tokens"] = VouchaApiEndpoints.RequestReAuthEmailToken();
    registry["native.auth.re-auth.email.verification"] = VouchaApiEndpoints.VerifyReAuthEmailCode("ABCDEFGHJK");
    registry["native.auth.re-auth.totp.verification"] = VouchaApiEndpoints.VerifyReAuthTotp("123456");
    registry["native.auth.re-auth.passkeys.options"] = VouchaApiEndpoints.ReAuthPasskeyOptions(ReAuthPasskeyId);
    registry["native.auth.re-auth.passkeys.verification"] = VouchaApiEndpoints.VerifyReAuthPasskey(
        ReAuthPasskeyId, ReAuthFlowId, JsonDocument.Parse(PasskeyAssertion).RootElement);
    registry["native.auth.re-auth.providers.challenge.google"] = VouchaApiEndpoints.ReAuthProviderChallenge("google");
    registry["native.auth.re-auth.providers.verification.google"] =
        VouchaApiEndpoints.VerifyReAuthProvider("google", ReAuthFlowId, "fixture-provider-credential");
    registry["native.auth.re-auth.providers.authorization.github"] =
        VouchaApiEndpoints.BeginReAuthProviderAuthorization("github", new string('Z', 43));
    registry["native.auth.re-auth.providers.proof.github"] =
        VouchaApiEndpoints.ProveReAuthProvider("github", ReAuthFlowId);
    registry["native.auth.session.refresh.rotated"] =
        VouchaApiEndpoints.RefreshSession("fixture-previous-device-jwt", "fixture-previous-session-jwt");
    registry["native.bot-challenge.required"] =
        VouchaApiEndpoints.ShareRssFeedItemWithFollowers("00000000-0000-7000-8000-000000000743");
    return registry;
  }

  private const string PasskeyAssertion = """
      {
        "clientExtensionResults": {},
        "id": "dm91Y2hhLXN5bnRoZXRpYy1wYXNza2V5LWNyZWRlbnRpYWw",
        "rawId": "dm91Y2hhLXN5bnRoZXRpYy1wYXNza2V5LWNyZWRlbnRpYWw",
        "response": {
          "authenticatorData": "AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQ",
          "clientDataJSON": "eyJ0eXBlIjoid2ViYXV0aG4uZ2V0IiwiY2hhbGxlbmdlIjoiUWtKQ1FrSkNRa0pDUWtKQ1FrSkNRa0pDUWtKQ1FrSkNRa0pDUWtKQ1FrSSIsIm9yaWdpbiI6Imh0dHBzOi8vdm91Y2hhLmFpIn0",
          "signature": "AgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg"
        },
        "type": "public-key"
      }
      """;
}

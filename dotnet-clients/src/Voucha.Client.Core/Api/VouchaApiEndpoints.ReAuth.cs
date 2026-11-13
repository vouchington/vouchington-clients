using System.Text.Json;

namespace Voucha.Client.Core.Api;

// Contract-only builders for fresh account confirmation (re-auth) and rotating session refresh.
// No settings or session flow calls them yet.
public static partial class VouchaApiEndpoints
{
  public static ApiRequest ReAuthMethods() => Get("/api/v1/auth/re-auth/methods");

  public static ApiRequest RequestReAuthEmailToken() =>
      new(HttpMethod.Post, "/api/v1/auth/mfa/re-auth/email/tokens");

  public static ApiRequest VerifyReAuthEmailCode(string code) =>
      new(HttpMethod.Post, "/api/v1/auth/mfa/re-auth/email/verification") { Body = new ReAuthCodeBody(Auth.EmailVerificationCode.RequireValid(code)) };

  public static ApiRequest VerifyReAuthTotp(string code) =>
      new(HttpMethod.Post, "/api/v1/auth/mfa/re-auth/totp/verification") { Body = new ReAuthCodeBody(code) };

  public static ApiRequest ReAuthPasskeyOptions(string passkeyId) =>
      new(HttpMethod.Post, "/api/v1/auth/re-auth/passkeys/options")
      {
        Body = new ReAuthPasskeyOptionsBody(passkeyId),
      };

  public static ApiRequest VerifyReAuthPasskey(string passkeyId, string flowId, JsonElement response) =>
      new(HttpMethod.Post, "/api/v1/auth/re-auth/passkeys/verification")
      {
        Body = new ReAuthPasskeyVerificationBody(passkeyId, flowId, response),
      };

  public static ApiRequest ReAuthProviderChallenge(string provider) =>
      new(HttpMethod.Post, $"/api/v1/auth/re-auth/providers/{Path(provider)}/challenges");

  public static ApiRequest VerifyReAuthProvider(string provider, string flowId, string credential) =>
      new(HttpMethod.Post, $"/api/v1/auth/re-auth/providers/{Path(provider)}/verification")
      {
        Body = new ReAuthProviderVerificationBody(flowId, credential),
      };

  public static ApiRequest BeginReAuthProviderAuthorization(string provider, string completionProofChallenge) =>
      new(HttpMethod.Post, $"/api/v1/auth/re-auth/providers/{Path(provider)}/authorizations")
      {
        Body = new ReAuthProviderAuthorizationBody("native", completionProofChallenge),
      };

  public static ApiRequest ProveReAuthProvider(string provider, string flowId) =>
      new(HttpMethod.Post, $"/api/v1/auth/re-auth/providers/{Path(provider)}/proofs")
      {
        Body = new ReAuthProviderProofBody(flowId),
      };

  public static ApiRequest RefreshSession(string deviceToken, string? sessionToken) =>
      new(HttpMethod.Patch, "/api/v1/session") { Body = new RefreshSessionBody(deviceToken, sessionToken) };
}

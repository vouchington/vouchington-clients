using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  public static Dictionary<string, Type> WithNativeSessionFixtures(this Dictionary<string, Type> registry)
  {
    registry["native.auth.email-address.tokens"] = typeof(NativeEmailTokenResponse);
    registry["native.auth.email-address.login.authenticated"] = typeof(NativeEmailLoginAuthenticatedResponse);
    registry["native.auth.email-address.login.mfa"] = typeof(NativeEmailLoginMfaResponse);
    registry["native.oauth.session-mcp-token.ios"] = typeof(NativeSessionMcpTokenResponse);
    return registry;
  }

  private static Dictionary<string, ApiRequest> WithNativeSessionEndpoints(
      this Dictionary<string, ApiRequest> registry)
  {
    registry["native.auth.email-address.tokens"] = VouchaApiEndpoints.RequestNativeEmailOtp("fixture@example.test");
    registry["native.auth.email-address.login.authenticated"] =
        VouchaApiEndpoints.VerifyNativeEmailOtp("fixture@example.test", "123456");
    registry["native.auth.email-address.login.mfa"] =
        VouchaApiEndpoints.VerifyNativeEmailOtp("fixture@example.test", "123456");
    registry["native.oauth.session-mcp-token.ios"] = VouchaApiEndpoints.MintNativeSessionMcpToken("ios");
    return registry;
  }
}

namespace Voucha.Client.Core.Api;

// Contract-only builders for the native body-token email login and session-to-MCP token mint.
// No sign-in or settings flow calls them yet.
public static partial class VouchaApiEndpoints
{
  public static ApiRequest RequestNativeEmailOtp(string email) =>
      new(HttpMethod.Post, "/api/v1/auth/native/email-address/tokens")
      {
        Body = new RequestNativeEmailOtpBody(email),
      };

  public static ApiRequest VerifyNativeEmailOtp(string email, string code) =>
      new(HttpMethod.Post, "/api/v1/auth/native/email-address/login")
      {
        Body = new VerifyEmailOtpBody(email, code),
      };

  public static ApiRequest MintNativeSessionMcpToken(string clientApp) =>
      new(HttpMethod.Post, "/api/v1/oauth/native-session-tokens")
      {
        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["x-voucha-native-mcp-mint"] = "1",
        },
        Body = new MintNativeSessionMcpTokenBody(clientApp),
      };
}

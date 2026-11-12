namespace Voucha.Client.Core.Api;

// Native body-token email login and session-to-MCP token mint. Native routes reject browser
// Origin and Sec-Fetch headers; the API client never adds them.
public static partial class VouchaApiEndpoints
{
  public static ApiRequest RequestNativeEmailOtp(
      string email, string? turnstileToken = null, string? uiLocale = null) =>
      new(HttpMethod.Post, "/api/v1/auth/native/email-address/tokens")
      {
        Body = new RequestNativeEmailOtpBody(email, turnstileToken, uiLocale),
      };

  public static ApiRequest VerifyNativeEmailOtp(string email, string code) =>
      new(HttpMethod.Post, "/api/v1/auth/native/email-address/login")
      {
        Body = new VerifyEmailOtpBody(email, Auth.EmailVerificationCode.RequireValid(code)),
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

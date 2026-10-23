namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest RequestNativeEmailOtp(string email) =>
      new(HttpMethod.Post, "/api/v1/auth/native/email-address/tokens")
      {
        Body = new { emailAddress = email },
      };

  public static ApiRequest VerifyNativeEmailOtp(string email, string token) =>
      new(HttpMethod.Post, "/api/v1/auth/native/email-address/login")
      {
        Body = new { emailAddress = email, token },
      };

  public static ApiRequest MintNativeSessionMcpToken(string clientApp) =>
      new(HttpMethod.Post, "/api/v1/oauth/native-session-tokens")
      {
        Body = new { client_app = clientApp },
        Headers = new Dictionary<string, string> { ["x-voucha-native-mcp-mint"] = "1" },
      };
}

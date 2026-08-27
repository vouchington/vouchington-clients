namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest RequestEmailOtp(string email, string turnstileToken, string? uiLocale = null) =>
      new(HttpMethod.Post, "/api/v1/auth/email-address/tokens")
      {
        Body = new RequestEmailOtpBody(email, turnstileToken, uiLocale),
      };

  public static ApiRequest VerifyEmailOtp(string email, string code) =>
      new(HttpMethod.Post, "/api/v1/auth/email-address/login")
      {
        Body = new VerifyEmailOtpBody(email, code),
      };

  public static ApiRequest VerifyMfaTotp(string loginAttemptId, string code) =>
      new(HttpMethod.Post, "/api/v1/auth/mfa/totp/verification")
      {
        Body = new VerifyMfaTotpBody(loginAttemptId, code),
      };

  public static ApiRequest PasskeyAuthOptions() =>
      new(HttpMethod.Post, "/api/v1/auth/passkeys/authentication/options");

  public static ApiRequest PasskeyAuthVerify(object response) =>
      new(HttpMethod.Post, "/api/v1/auth/passkeys/authentication/verify")
      {
        Body = new PasskeyAuthVerifyBody(response),
      };

  public static ApiRequest AppleSignIn(string token, string? nonce, string? userName) =>
      new(HttpMethod.Post, "/api/v1/auth/oauth/apple/continue")
      {
        Body = new AppleSignInBody(token, nonce, userName),
      };
}

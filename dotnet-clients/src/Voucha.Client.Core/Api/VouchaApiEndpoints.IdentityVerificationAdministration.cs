namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest GrantIdentityVerificationAttempt(
      string userId,
      GrantIdentityVerificationAttemptBody body) =>
      new(HttpMethod.Post, $"/api/v1/admin/users/{Path(userId)}/identity-verification-attempts") { Body = body };
}

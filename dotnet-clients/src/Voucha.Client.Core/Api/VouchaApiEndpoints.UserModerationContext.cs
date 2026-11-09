namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest UserModerationContext(string userId) =>
      Get($"/api/v1/users/{Path(userId)}/moderation-context");
}

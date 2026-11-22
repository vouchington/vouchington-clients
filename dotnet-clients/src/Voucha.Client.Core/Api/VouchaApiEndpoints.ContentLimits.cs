namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest ContentLimits() => Get("/api/v1/content-limits");
}

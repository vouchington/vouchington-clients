namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest Currencies(string? after = null, int? limit = null) =>
      Get("/api/v1/currencies", Query(("limit", limit), ("after", after)));
}

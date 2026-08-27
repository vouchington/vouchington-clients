namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest AiCosts(string? after = null) =>
      Get("/api/v1/admin/ai-costs", Query(("limit", 25), ("after", after)));
}

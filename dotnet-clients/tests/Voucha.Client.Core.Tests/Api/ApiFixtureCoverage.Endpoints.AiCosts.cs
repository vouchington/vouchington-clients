using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  private static Dictionary<string, ApiRequest> WithAiCostEndpoints(Dictionary<string, ApiRequest> endpoints)
  {
    endpoints["native.admin-ai-costs.default"] = VouchaApiEndpoints.AiCosts();
    endpoints["native.admin-ai-costs.page-2"] = VouchaApiEndpoints.AiCosts(
        ApiFixtureLoader.QueryValue("native.admin-ai-costs.page-2", "after"));
    endpoints["native.admin-ai-costs.empty"] = VouchaApiEndpoints.AiCosts();
    return endpoints;
  }
}

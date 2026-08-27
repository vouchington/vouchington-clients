using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class ApiFixtureEndpointCoverageTests
{
  private static Dictionary<string, ApiRequest> WithAiCostEndpoints(Dictionary<string, ApiRequest> registry)
  {
    registry["native.admin-ai-costs.default"] = VouchaApiEndpoints.AiCosts();
    registry["native.admin-ai-costs.page-2"] = VouchaApiEndpoints.AiCosts(
        ApiFixtureLoader.QueryValue("native.admin-ai-costs.page-2", "after"));
    registry["native.admin-ai-costs.empty"] = VouchaApiEndpoints.AiCosts();
    return registry;
  }
}

using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed class ScopeAudiencePolicyContractTests
{
  [Fact]
  public void PublishedCatalogKeepsAudienceApiKeyPolicy()
  {
    var catalog = JsonSerializer.Deserialize<ScopeCatalogResponse>(
        ApiFixtureLoader.LoadResponse("shared.scopes.catalog"), VouchaApiJson.Options)!;

    Assert.Equal(
        [
            ("user", true, false),
            ("community_moderation", true, false),
            ("brand", true, false),
            ("admin", false, true),
            ("legal", false, true),
        ],
        catalog.Audiences.Select(policy =>
            (policy.Audience, policy.AcceptsApiKeys, policy.RequiresAdministrator)).ToArray());
  }
}

using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed class ApiSettingsServiceOAuthManagementTests
{
  [Fact]
  public async Task CredentialWrappersDecodeFixturesAndForwardScopeCursorAndGrantIdentity()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(ApiFixtureLoader.LoadResponse("shared.scopes.catalog")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.my.oauth-grants.paginated")),
        new RecordedResponse("null", System.Net.HttpStatusCode.NoContent),
    ]);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiSettingsService(new VouchaApiClient(http));

    var catalog = await service.FetchScopeCatalogAsync(TestContext.Current.CancellationToken);
    var grants = await service.FetchOAuthGrantsAsync("opaque/+cursor", 7, TestContext.Current.CancellationToken);
    var grant = Assert.Single(grants.Results);
    await service.RevokeOAuthGrantAsync(grant.Id, TestContext.Current.CancellationToken);

    Assert.Contains(catalog.Scopes, scope => scope.Scope == "mcp.user:write" && scope.Requires == "mcp.user:read");
    Assert.True(grant.Client.Verified);
    Assert.Equal("Fixture Agent", grant.Client.ClientName);
    Assert.Equal("fixture-oauth-grant-end-cursor", grants.PageInfo.EndCursor);
    Assert.Collection(handler.Requests,
        request => Assert.Equal("/api/v1/scopes", request.PathAndQuery),
        request => Assert.Equal("/api/v1/my/oauth-grants?after=opaque%2F%2Bcursor&limit=7", request.PathAndQuery),
        request =>
        {
          Assert.Equal(HttpMethod.Delete, request.Method);
          Assert.Equal($"/api/v1/my/oauth-grants/{grant.Id}", request.PathAndQuery);
        });
  }
}

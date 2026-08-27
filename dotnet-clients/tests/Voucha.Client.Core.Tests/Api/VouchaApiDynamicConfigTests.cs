using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiDynamicConfigTests
{
  [Fact]
  public void EndpointsEncodeNamespaceAndPatchOneField()
  {
    var request = VouchaApiEndpoints.UpdateDynamicConfigField("feature flags", "fediverse", DynamicConfigValues.From(false));

    Assert.Equal(HttpMethod.Patch, request.Method);
    Assert.Equal("/api/v1/dynamic-config/namespaces/feature%20flags", request.Path);
    var body = Assert.IsType<DynamicConfigPatchBody>(request.Body);
    var value = Assert.IsType<DynamicConfigBooleanValue>(Assert.Single(body.Config).Value);
    Assert.False(value.Value);
  }

  [Fact]
  public void AllEightFixturesRoundTripStrictModels()
  {
    var fixtureTypes = new Dictionary<string, Type>
    {
      ["native.feature-flags.default"] = typeof(FeatureFlagsResponse),
      ["native.captcha-config.default"] = typeof(CaptchaConfigResponse),
      ["native.dynamic-config.namespaces.developer"] = typeof(DynamicConfigNamespacesResponse),
      ["native.dynamic-config.namespace.typed"] = typeof(DynamicConfigNamespaceResponse),
      ["native.dynamic-config.namespace.string"] = typeof(DynamicConfigNamespaceResponse),
      ["native.dynamic-config.namespace.integer"] = typeof(DynamicConfigNamespaceResponse),
      ["native.dynamic-config.update.changed"] = typeof(DynamicConfigUpdateResponse),
      ["native.dynamic-config.update.no-op"] = typeof(DynamicConfigUpdateResponse),
      ["native.dynamic-config.history.default"] = typeof(DynamicConfigHistoryResponse),
    };

    foreach (var fixture in fixtureTypes)
    {
      var value = JsonSerializer.Deserialize(ApiFixtureLoader.LoadResponse(fixture.Key), fixture.Value, VouchaApiJson.Options);
      Assert.NotNull(value);
    }

    var integerNamespace = JsonSerializer.Deserialize<DynamicConfigNamespaceResponse>(
        ApiFixtureLoader.LoadResponse("native.dynamic-config.namespace.integer"), VouchaApiJson.Options);
    Assert.Equal(
        "The effective ceiling is bounded by the configured contribution payload size.",
        integerNamespace?.Namespace.Fields[1].MaxValueExemption);
    var stringNamespace = JsonSerializer.Deserialize<DynamicConfigNamespaceResponse>(
        ApiFixtureLoader.LoadResponse("native.dynamic-config.namespace.string"), VouchaApiJson.Options);
    Assert.Null(stringNamespace?.Namespace.Fields.Single(field => field.Name == "request_signing_mode").DefaultValue);
  }
}

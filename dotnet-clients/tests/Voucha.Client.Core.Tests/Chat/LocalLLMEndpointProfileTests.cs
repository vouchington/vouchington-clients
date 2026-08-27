using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class LocalLLMEndpointProfileTests
{
  [Fact]
  public async Task ProfilesKeepSecretsScopedToTheirUuidAndClearThemOnDeletion()
  {
    var first = Guid.NewGuid();
    var second = Guid.NewGuid();
    var secrets = new InMemoryLocalLLMSecretStore();
    await secrets.SaveApiKeyAsync(first, "first", TestContext.Current.CancellationToken);
    await secrets.SaveApiKeyAsync(second, "second", TestContext.Current.CancellationToken);

    await secrets.ClearApiKeyAsync(first, TestContext.Current.CancellationToken);

    Assert.Null(await secrets.ReadApiKeyAsync(first, TestContext.Current.CancellationToken));
    Assert.Equal("second", await secrets.ReadApiKeyAsync(second, TestContext.Current.CancellationToken));
  }

  [Fact]
  public void ResponsesUriAppliesTheSharedEndpointPolicy()
  {
    var contract = LocalLLMEndpointPolicyContract.Load();
    var rows = contract.HostPolicyRowsFor("dotnet-core");
    Assert.Equal(ExpectedHostPolicyRowIds, rows.Select(row => row.Id).ToHashSet());

    foreach (var row in rows)
    {
      var result = LocalLLMEndpointProfile.TryBuildResponsesUri(row.Endpoint);
      if (row.IsAllowed)
      {
        Assert.NotNull(result);
      }
      else
      {
        Assert.Null(result);
      }
    }
  }

  // Drives the extracted IPAddress overload directly, mirroring the hostname corpus above, so the
  // connect-time re-validation in LocalLLMConnectionPinning shares proof against the same classes
  // of address without going through URI parsing.
  [Theory]
  [InlineData("127.0.0.1", true)]
  [InlineData("192.168.1.20", true)]
  [InlineData("8.8.8.8", false)]
  [InlineData("::1", true)]
  [InlineData("fc00::8", true)]
  [InlineData("fe80::8", true)]
  [InlineData("2001:4860:4860::8888", false)]
  [InlineData("::ffff:127.0.0.1", true)]
  [InlineData("::ffff:192.168.1.1", false)]
  [InlineData("100.64.0.1", false)]
  [InlineData("100.100.100.100", false)]
  [InlineData("100.127.255.255", false)]
  [InlineData("100.63.255.255", false)]
  [InlineData("100.128.0.0", false)]
  public void IsPrivateNetworkHostAppliesTheSharedRangePolicyToResolvedAddresses(string address, bool isPrivate)
  {
    Assert.Equal(isPrivate, LocalLLMNetworkPolicy.IsPrivateNetworkHost(IPAddress.Parse(address)));
  }

  [Fact]
  public void HostPolicyRowRejectsUnknownVerdict()
  {
    using var document = JsonDocument.Parse(
        """{"id":"typo","requiredConsumers":["dotnet-core"],"endpoint":"http://127.0.0.1:11434","verdict":"rejetced","notes":"misspelled"}""");

    var error = Assert.Throws<JsonException>(
        () => LocalLLMEndpointPolicyContract.ParseHostPolicyRow(document.RootElement));

    Assert.Contains("rejetced", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void HostPolicyRowRejectsUnknownConsumer()
  {
    using var document = JsonDocument.Parse(
        """{"id":"typo","requiredConsumers":["swift-croe"],"endpoint":"http://127.0.0.1:11434","verdict":"allowed","notes":"misspelled"}""");

    var error = Assert.Throws<JsonException>(
        () => LocalLLMEndpointPolicyContract.ParseHostPolicyRow(document.RootElement));

    Assert.Contains("swift-croe", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void EndpointOriginAppliesTheSharedCanonicalizationPolicy()
  {
    var contract = LocalLLMEndpointPolicyContract.Load();
    var pairs = contract.OriginPairsFor("dotnet-core");
    Assert.Equal(ExpectedOriginPairIds, pairs.Select(pair => pair.Id).ToHashSet());

    foreach (var pair in pairs)
    {
      var originA = new LocalLLMEndpointProfile(Guid.NewGuid(), "Test", true, pair.VariantA).Origin;
      var originB = new LocalLLMEndpointProfile(Guid.NewGuid(), "Test", true, pair.VariantB).Origin;
      Assert.Equal(pair.ExpectedOrigin, originA);
      Assert.Equal(pair.ExpectedOrigin, originB);
    }
  }

  private static readonly HashSet<string> ExpectedHostPolicyRowIds =
  [
    "loopback-ipv4", "rfc1918-10-private", "rfc1918-172-private", "rfc1918-192-private",
    "link-local-ipv4", "loopback-ipv6", "ula-ipv6", "link-local-ipv6", "dot-local-hostname",
    "single-label-hostname", "localhost-name", "cgnat-100-64-rejected", "cgnat-100-128-rejected",
    "public-ipv4-rejected", "public-ipv6-rejected",
    "public-hostname-rejected", "zone-id-private-looking-rejected",
    "zone-id-percent-encoded-private-looking-rejected", "zone-id-public-rejected",
    "ipv4-mapped-loopback-rejected", "ipv4-mapped-private-rejected", "ipv4-mapped-public-rejected",
    "non-http-scheme-rejected", "malformed-url-rejected", "userinfo-present-rejected",
    "query-present-rejected", "fragment-present-rejected", "https-public-host-allowed",
    "https-cgnat-100-64-allowed", "https-zone-id-rejected",
  ];

  private static readonly HashSet<string> ExpectedOriginPairIds =
  [
    "https-default-vs-explicit-port", "http-default-vs-explicit-port",
    "ipv6-default-vs-explicit-port-http", "ipv6-default-vs-explicit-port-https",
  ];

  [Fact]
  public async Task ChangingProfileOriginClearsOnlyThatProfilesSecret()
  {
    var profileId = Guid.NewGuid();
    var untouchedId = Guid.NewGuid();
    var secrets = new InMemoryLocalLLMSecretStore();
    await secrets.SaveApiKeyAsync(profileId, "changed", TestContext.Current.CancellationToken);
    await secrets.SaveApiKeyAsync(untouchedId, "untouched", TestContext.Current.CancellationToken);
    var previous = new LocalLLMEndpointProfile(profileId, "One", true, "http://127.0.0.1:11434", ["model"], "model");
    var changed = previous with { Endpoint = "http://127.0.0.1:1234" };

    if (previous.Origin != changed.Origin) await secrets.ClearApiKeyAsync(profileId, TestContext.Current.CancellationToken);

    Assert.Null(await secrets.ReadApiKeyAsync(profileId, TestContext.Current.CancellationToken));
    Assert.Equal("untouched", await secrets.ReadApiKeyAsync(untouchedId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public void ExplicitSelectedProviderIsNotReplacedWhenItsProfileIsMissing()
  {
    var selected = LocalChatProviderIds.OpenAICompatible(Guid.NewGuid());
    var configuration = new LocalLLMConfiguration([], null, selected);

    Assert.Null(configuration.SelectedEndpoint);
    Assert.Equal(selected, configuration.SelectedProviderId);
  }
}

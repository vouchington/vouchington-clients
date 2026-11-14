using Voucha.Client.Core;
using Xunit;
using System.Reflection;

[assembly: AssemblyMetadata("VOUCHA_API_BASE_URL", "https://packaged.example.test")]
[assembly: AssemblyMetadata("VOUCHA_TURNSTILE_SITE_KEY", "assembly-public-site-key")]

namespace Voucha.Client.Core.Tests;

public sealed class PackagedAppConfigTests
{
  [Fact]
  public void AppAssemblyMetadataFlowsThroughTheProductionResolver()
  {
    var config = AppConfig.FromAssembly(typeof(PackagedAppConfigTests).Assembly, new Dictionary<string, string?>());

    Assert.Equal(new Uri("https://packaged.example.test"), config.ApiBaseUrl);
    Assert.Equal("assembly-public-site-key", config.RequiredTurnstileSiteKey);
  }

  [Fact]
  public void PackagedConfigurationWorksWithoutProcessEnvironment()
  {
    var config = AppConfig.FromEnvironment(new Dictionary<string, string?>(), new Dictionary<string, string?>
    {
      ["VOUCHA_API_BASE_URL"] = "https://staging.example.test",
      ["VOUCHA_TURNSTILE_SITE_KEY"] = "packaged-public-site-key",
    });

    Assert.Equal(new Uri("https://staging.example.test"), config.ApiBaseUrl);
    Assert.Equal("packaged-public-site-key", config.RequiredTurnstileSiteKey);
    Assert.False(config.AllowPostComposeCaptchaBypass);
  }

  [Fact]
  public void EnvironmentOverridesPackagedValuesIncludingSharedPublicKey()
  {
    var config = AppConfig.FromEnvironment(new Dictionary<string, string?>
    {
      ["VOUCHA_API_BASE_URL"] = "https://development.example.test",
      ["NEXT_PUBLIC_CLOUDFLARE_TURNSTILE_SITE_KEY"] = "environment-public-site-key",
    }, new Dictionary<string, string?>
    {
      ["VOUCHA_API_BASE_URL"] = "https://voucha.ai",
      ["VOUCHA_TURNSTILE_SITE_KEY"] = "packaged-public-site-key",
    });

    Assert.Equal(new Uri("https://development.example.test"), config.ApiBaseUrl);
    Assert.Equal("environment-public-site-key", config.TurnstileSiteKey);
  }

  [Fact]
  public void EmptyConfigurationUsesProductionApiAndResolvesTurnstileForTheCompiledConfiguration()
  {
    var config = AppConfig.FromEnvironment(new Dictionary<string, string?>
    {
      ["VOUCHA_API_BASE_URL"] = " ",
    }, new Dictionary<string, string?>
    {
      ["VOUCHA_API_BASE_URL"] = " ",
      ["VOUCHA_TURNSTILE_SITE_KEY"] = "\n",
    });

    Assert.Equal(new Uri(AppConfig.DefaultApiBaseUrl), config.ApiBaseUrl);
#if DEBUG
    Assert.Equal(TurnstileSiteKeys.AlwaysPassSiteKey, config.RequiredTurnstileSiteKey);
#else
    var exception = Assert.Throws<InvalidOperationException>(() => config.RequiredTurnstileSiteKey);
    Assert.Equal(TurnstileSiteKeys.MissingReleaseKeyMessage, exception.Message);
#endif
  }
}

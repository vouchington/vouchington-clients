using Voucha.Client.Core;
using Xunit;

namespace Voucha.Client.Core.Tests;

public sealed class AppConfigTests
{
  [Fact]
  public void FromEnvironmentUsesProductionDefaultsWithConfiguredTurnstile()
  {
    var config = AppConfig.FromEnvironment(new Dictionary<string, string?>(StringComparer.Ordinal)
    {
      ["VOUCHA_TURNSTILE_SITE_KEY"] = "site-key",
    });

    Assert.Equal(new Uri(AppConfig.DefaultApiBaseUrl), config.ApiBaseUrl);
    Assert.Equal(new Uri(AppConfig.DefaultImageBaseUrl), config.ImageBaseUrl);
    Assert.Equal(new Uri(AppConfig.DefaultWebBaseUrl), config.WebBaseUrl);
    Assert.Equal("site-key", config.TurnstileSiteKey);
    Assert.False(config.AllowPostComposeCaptchaBypass);
    Assert.False(config.AllowCommunityCreateCaptchaBypass);
    Assert.Null(config.AppleClientId);
  }

  [Fact]
  public void FromEnvironmentAllowsMissingTurnstileUntilCaptchaFlow()
  {
    var config = AppConfig.FromEnvironment(new Dictionary<string, string?>(StringComparer.Ordinal));

    Assert.Equal("", config.TurnstileSiteKey);
    var exception = Assert.Throws<InvalidOperationException>(() => config.RequiredTurnstileSiteKey);
    Assert.Equal("Missing VOUCHA_TURNSTILE_SITE_KEY", exception.Message);
  }

  [Fact]
  public void FromEnvironmentReadsNativeClientOverrides()
  {
    var config = AppConfig.FromEnvironment(new Dictionary<string, string?>(StringComparer.Ordinal)
    {
      ["VOUCHA_API_BASE_URL"] = "https://api.example.test",
      ["VOUCHA_IMAGE_BASE_URL"] = "https://images.example.test",
      ["VOUCHA_WEB_BASE_URL"] = "https://www.example.test",
      ["VOUCHA_TURNSTILE_SITE_KEY"] = "site-key",
      ["VOUCHA_POST_COMPOSE_CAPTCHA_BYPASS"] = "true",
      ["VOUCHA_COMMUNITY_CREATE_CAPTCHA_BYPASS"] = "true",
      ["VOUCHA_APPLE_CLIENT_ID"] = "com.example.apple",
    });

    Assert.Equal(new Uri("https://api.example.test"), config.ApiBaseUrl);
    Assert.Equal(new Uri("https://images.example.test"), config.ImageBaseUrl);
    Assert.Equal(new Uri("https://www.example.test"), config.WebBaseUrl);
    Assert.Equal("site-key", config.TurnstileSiteKey);
    Assert.True(config.AllowPostComposeCaptchaBypass);
    Assert.True(config.AllowCommunityCreateCaptchaBypass);
    Assert.Equal("com.example.apple", config.AppleClientId);
  }

  [Fact]
  public void FromEnvironmentReadsSharedWebRuntimePublicConfig()
  {
    var config = AppConfig.FromEnvironment(new Dictionary<string, string?>(StringComparer.Ordinal)
    {
      ["VOUCHA_API_BASE_URL"] = "https://api.example.test",
      ["SITEMAP_BASE_URL"] = "https://www.example.test",
      ["NEXT_PUBLIC_CLOUDFLARE_TURNSTILE_SITE_KEY"] = "shared-site-key",
      ["NEXT_PUBLIC_APPLE_CLIENT_ID"] = "web.apple.client",
    });

    Assert.Equal(new Uri("https://www.example.test"), config.WebBaseUrl);
    Assert.Equal("shared-site-key", config.TurnstileSiteKey);
    Assert.Equal("web.apple.client", config.AppleClientId);
  }

  [Fact]
  public void FromEnvironmentRejectsInvalidApiBaseUrl()
  {
    var exception = Assert.Throws<InvalidOperationException>(() =>
        AppConfig.FromEnvironment(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
          ["VOUCHA_API_BASE_URL"] = "not a url",
          ["VOUCHA_TURNSTILE_SITE_KEY"] = "site-key",
        }));

    Assert.Equal("Invalid VOUCHA_API_BASE_URL: not a url", exception.Message);
  }

  [Fact]
  public void FromEnvironmentRejectsInvalidImageBaseUrl()
  {
    var exception = Assert.Throws<InvalidOperationException>(() =>
        AppConfig.FromEnvironment(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
          ["VOUCHA_IMAGE_BASE_URL"] = "not a url",
          ["VOUCHA_TURNSTILE_SITE_KEY"] = "site-key",
        }));

    Assert.Equal("Invalid VOUCHA_IMAGE_BASE_URL: not a url", exception.Message);
  }

  [Fact]
  public void FromEnvironmentRejectsInvalidWebBaseUrl()
  {
    var exception = Assert.Throws<InvalidOperationException>(() =>
        AppConfig.FromEnvironment(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
          ["VOUCHA_WEB_BASE_URL"] = "not a url",
          ["VOUCHA_TURNSTILE_SITE_KEY"] = "site-key",
        }));

    Assert.Equal("Invalid VOUCHA_WEB_BASE_URL: not a url", exception.Message);
  }

  [Fact]
  public void ImageUrlForImageIdUsesConfiguredImageBaseUrl()
  {
    var config = new AppConfig(
        new Uri("https://api.example.test"),
        "site-key",
        ImageBaseUrl: new Uri("https://images.example.test/assets"));

    Assert.Equal(
        new Uri("https://images.example.test/assets/images/avatar-1?w=144"),
        config.ImageUrlForImageId("avatar-1", 144));
    Assert.Null(config.ImageUrlForImageId(" "));
  }
}

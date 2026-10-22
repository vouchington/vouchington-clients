using Voucha.Client.Core;
using Xunit;

namespace Voucha.Client.Core.Tests;

public sealed class TurnstileSiteKeyTests
{
  private const string ReleaseSiteKey = "0x4AAAAAAAAexample";

  [Fact]
  public void DebugResolvesAMissingKeyToTheAlwaysPassSiteKey()
  {
    Assert.Equal(TurnstileSiteKeys.AlwaysPassSiteKey, TurnstileSiteKeys.Resolve(null, release: false));
    Assert.Equal(TurnstileSiteKeys.AlwaysPassSiteKey, TurnstileSiteKeys.Resolve(" \n", release: false));
  }

  [Fact]
  public void DebugKeepsAnExplicitOverride()
  {
    Assert.Equal(ReleaseSiteKey, TurnstileSiteKeys.Resolve(ReleaseSiteKey, release: false));
    Assert.Equal(
        "2x00000000000000000000AB",
        TurnstileSiteKeys.Resolve("2x00000000000000000000AB", release: false));
  }

  [Fact]
  public void ReleaseRejectsAMissingKeyAndEveryCloudflareTestKey()
  {
    var missing = Assert.Throws<InvalidOperationException>(() => TurnstileSiteKeys.Resolve(" \t", release: true));
    Assert.Equal(TurnstileSiteKeys.MissingReleaseKeyMessage, missing.Message);

    foreach (var testKey in TurnstileSiteKeys.TestSiteKeys)
    {
      var rejected = Assert.Throws<InvalidOperationException>(
          () => TurnstileSiteKeys.Resolve($" {testKey} ", release: true));
      Assert.Equal(TurnstileSiteKeys.RejectedTestKeyMessage, rejected.Message);
    }
  }

  [Fact]
  public void ReleaseAcceptsAnExplicitNonTestKey()
  {
    Assert.Equal(ReleaseSiteKey, TurnstileSiteKeys.Resolve($" {ReleaseSiteKey} ", release: true));
    var config = AppConfig.FromEnvironment(new Dictionary<string, string?>
    {
      ["VOUCHA_TURNSTILE_SITE_KEY"] = ReleaseSiteKey,
    });
    Assert.Equal(ReleaseSiteKey, config.RequiredTurnstileSiteKey);
  }
}

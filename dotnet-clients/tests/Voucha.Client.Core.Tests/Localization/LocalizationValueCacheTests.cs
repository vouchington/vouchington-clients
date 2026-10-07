using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Localization;

public sealed class LocalizationValueCacheTests
{
  [Fact]
  public void OverlayHonorsTtlAndKeepsStaleValues()
  {
    var cache = new LocalizationValueCache();
    var now = DateTimeOffset.UnixEpoch.AddMinutes(1);
    cache.Apply("en", "rev-1", 60, new Dictionary<string, string> { ["common.cancel"] = "Abort" }, now);

    Assert.Equal("Abort", cache.Value("common.cancel", "en"));
    Assert.Equal("\"rev-1\"", cache.Etag("en"));
    Assert.False(cache.IsExpired("en", now.AddSeconds(59)));
    Assert.True(cache.IsExpired("en", now.AddSeconds(60)));
    Assert.Equal("Abort", cache.Value("common.cancel", "en"));
  }

  [Fact]
  public void RememberNotModifiedExtendsExpiry()
  {
    var cache = new LocalizationValueCache();
    var now = DateTimeOffset.UnixEpoch;
    cache.Apply("en", "rev-1", 10, new Dictionary<string, string> { ["common.cancel"] = "Abort" }, now);
    cache.RememberNotModified("en", now.AddSeconds(10));

    Assert.False(cache.IsExpired("en", now.AddSeconds(11)));
    Assert.True(cache.IsExpired("en", now.AddSeconds(20)));
    Assert.Equal("Abort", cache.Value("common.cancel", "en"));
  }

  [Fact]
  public void LruEvictsOldestLocaleWhenByteBoundIsExceeded()
  {
    var cache = new LocalizationValueCache(8);
    cache.Apply("en", "a", 60, new Dictionary<string, string> { ["a"] = "12345" }, DateTimeOffset.UnixEpoch);
    cache.Apply("es", "b", 60, new Dictionary<string, string> { ["b"] = "67890" }, DateTimeOffset.UnixEpoch);

    Assert.Null(cache.Value("a", "en"));
    Assert.Equal("67890", cache.Value("b", "es"));
  }

  [Fact]
  public void LruEvictsLocaleWhenItsPayloadAloneExceedsByteBound()
  {
    var cache = new LocalizationValueCache(4);
    cache.Apply("en", "a", 60, new Dictionary<string, string> { ["a"] = "12345" }, DateTimeOffset.UnixEpoch);
    Assert.Null(cache.Value("a", "en"));
  }
}

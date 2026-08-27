using System.Text.Json;
using Voucha.Client.Core.FeatureFlags;
using Xunit;

namespace Voucha.Client.Core.Tests.FeatureFlags;

public sealed class FileFeatureFlagOverrideStoreTests
{
  [Fact]
  public async Task ConcurrentSavesLeaveOneCompleteAtomicDocument()
  {
    var token = TestContext.Current.CancellationToken;
    var directory = TemporaryDirectory();
    try
    {
      var path = Path.Combine(directory, "overrides.json");
      using var store = new FileFeatureFlagOverrideStore(path);
      var first = new Dictionary<string, bool> { ["fediverse"] = true };
      var second = new Dictionary<string, bool> { ["chat"] = false, ["support"] = true };

      await Task.WhenAll(store.SaveAsync(first, token), store.SaveAsync(second, token));

      var persisted = JsonSerializer.Deserialize<Dictionary<string, bool>>(await File.ReadAllTextAsync(path, token));
      Assert.True(DictionaryEqual(first, persisted) || DictionaryEqual(second, persisted));
      Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
    }
    finally
    {
      Directory.Delete(directory, recursive: true);
    }
  }

  [Fact]
  public async Task CorruptFileRecoversAndFreshStateRetainsAccountIndependentOverride()
  {
    var token = TestContext.Current.CancellationToken;
    var directory = TemporaryDirectory();
    try
    {
      var path = Path.Combine(directory, "overrides.json");
      await File.WriteAllTextAsync(path, "not json", token);
      using (var corruptStore = new FileFeatureFlagOverrideStore(path))
      {
        Assert.Empty(await corruptStore.LoadAsync(token));
        await corruptStore.SaveAsync(new Dictionary<string, bool> { ["fediverse"] = false }, token);
      }

      using var freshStore = new FileFeatureFlagOverrideStore(path);
      using var freshState = new FeatureFlagState(freshStore);
      await freshState.InitializeAsync(token);
      await freshState.ApplyAuthoritativeRemoteAsync(new Dictionary<string, bool> { ["fediverse"] = true }, token);

      Assert.False(freshState.EffectiveFlags["fediverse"]);
      Assert.False(freshState.LocalOverrides["fediverse"]);
    }
    finally
    {
      Directory.Delete(directory, recursive: true);
    }
  }

  private static string TemporaryDirectory()
  {
    var path = Path.Combine(Path.GetTempPath(), $"voucha-feature-flags-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);
    return path;
  }

  private static bool DictionaryEqual(
      IReadOnlyDictionary<string, bool> expected,
      IReadOnlyDictionary<string, bool>? actual) =>
      actual is not null && expected.Count == actual.Count && expected.All(pair =>
          actual.TryGetValue(pair.Key, out var value) && value == pair.Value);
}

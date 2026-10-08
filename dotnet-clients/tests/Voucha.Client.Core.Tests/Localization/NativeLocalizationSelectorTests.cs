using System.Reflection;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Localization;

public sealed class NativeLocalizationSelectorTests
{
  [Fact]
  public void RequestedSelectorsCoverEveryGeneratedDotnetMessageKey()
  {
    var keys = typeof(UiMessageKey).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => ((UiMessageKey)field.GetValue(null)!).Value).ToArray();
    Assert.NotEmpty(keys);
    var omitted = keys.Where(key => !NativeLocalizationSelectors.Chrome.Any(selector =>
        selector.EndsWith(".*", StringComparison.Ordinal)
            ? key.StartsWith(selector[..^1], StringComparison.Ordinal)
            : key == selector)).ToArray();

    Assert.Empty(omitted);
  }
}

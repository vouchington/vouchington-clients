using Xunit;

namespace Voucha.Client.Core.Tests.Api;

internal static class ApiQueryAssertions
{
  public static void Equal(string expected, string? actual)
  {
    var expectedParts = expected.Split('?', 2);
    var actualParts = Assert.IsType<string>(actual).Split('?', 2);
    Assert.Equal(expectedParts[0], actualParts[0]);
    Assert.Equal(Parameters(expectedParts), Parameters(actualParts));
  }

  private static IEnumerable<string> Parameters(string[] parts) =>
      parts.Length == 1 ? [] : parts[1].Split('&').Order(StringComparer.Ordinal);
}

using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Support;

public sealed class TrimmedTextTests
{
  [Theory]
  [InlineData(null, null)]
  [InlineData("", null)]
  [InlineData(" ", null)]
  [InlineData("\t\r\n", null)]
  [InlineData("\u00a0", null)]
  [InlineData(" kept ", "kept")]
  [InlineData("a b", "a b")]
  public void DropsBlankTextAndTrimsTheRest(string? value, string? expected)
  {
    Assert.Equal(expected, TrimmedText.NonEmpty(value));
  }
}

using Voucha.Client.Core.Content;
using Xunit;

namespace Voucha.Client.Core.Tests.Content;

public sealed class AuthoredContentLanguageTests
{
  [Theory]
  [InlineData("ar", "en", "ar", AuthoredTextDirection.RightToLeft)]
  [InlineData(null, "he", "he", AuthoredTextDirection.RightToLeft)]
  [InlineData("fr-CA", "ar", "fr", AuthoredTextDirection.LeftToRight)]
  [InlineData("ar_EG", null, "ar", AuthoredTextDirection.RightToLeft)]
  [InlineData("en_US", null, "en", AuthoredTextDirection.LeftToRight)]
  [InlineData(null, "en", "en", AuthoredTextDirection.LeftToRight)]
  public void Resolve_prefers_declared_language_and_returns_leaf_direction(
      string? declared, string? detected, string expectedTag, AuthoredTextDirection expectedDirection)
  {
    var result = AuthoredContentLanguage.Resolve(declared, detected);

    Assert.Equal(expectedTag, result.Tag);
    Assert.Equal(expectedDirection, result.Direction);
  }

  [Theory]
  [InlineData(null, null)]
  [InlineData("", "  ")]
  [InlineData("invalid_language", "also_invalid")]
  [InlineData("dv", null)]
  public void Resolve_leaves_missing_or_invalid_language_unset(string? declared, string? detected)
  {
    var result = AuthoredContentLanguage.Resolve(declared, detected);

    Assert.Null(result.Tag);
    Assert.Null(result.Direction);
  }
}

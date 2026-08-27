using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class NotificationTargetPolicyTests
{
  [Theory]
  [InlineData("topics")]
  [InlineData("/topics")]
  [InlineData("voucha://topics")]
  public void TryResolveTreatsRelativeAndVouchaTargetsAsInternalAppLinks(string input)
  {
    var matched = NotificationTargetPolicy.TryResolve(input, out var resolution);

    Assert.True(matched);
    Assert.NotNull(resolution);
    Assert.True(resolution!.IsInternalAppLink);
    Assert.Equal("voucha", resolution.Uri.Scheme);
    Assert.Equal("topics", resolution.Uri.Host);
    Assert.Equal("/", resolution.Uri.AbsolutePath);
  }

  [Theory]
  [InlineData("https://example.com/topics")]
  [InlineData("http://example.com/topics")]
  public void TryResolveTreatsHttpTargetsAsExternalWebLinks(string input)
  {
    var matched = NotificationTargetPolicy.TryResolve(input, out var resolution);

    Assert.True(matched);
    Assert.NotNull(resolution);
    Assert.True(resolution!.IsExternalWebLink);
    Assert.Equal(input[..input.IndexOf(':')], resolution.Uri.Scheme);
    Assert.Equal("example.com", resolution.Uri.Host);
    Assert.Equal("/topics", resolution.Uri.AbsolutePath);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("file:///tmp/topic")]
  [InlineData("bad uri")]
  [InlineData("/topics:feed")]
  public void TryResolveRejectsBlankInvalidAndFileTargets(string? input)
  {
    var matched = NotificationTargetPolicy.TryResolve(input, out var resolution);

    Assert.False(matched);
    Assert.Null(resolution);
  }
}

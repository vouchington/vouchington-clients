using System.Globalization;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class VoteClearEligibilityConverterTests
{
  [Fact]
  public void ConvertRequiresBothABallotAndSessionClearPermission()
  {
    var converter = new VoteClearEligibilityConverter();

    Assert.True(Convert(converter, [ElectionVoteChoice.Vouch, true]));
    Assert.False(Convert(converter, [null, true]));
    Assert.False(Convert(converter, [ElectionVoteChoice.Vouch, false]));
    Assert.False(Convert(converter, [ElectionVoteChoice.Vouch]));
  }

  private static bool Convert(VoteClearEligibilityConverter converter, object?[] values) =>
      Assert.IsType<bool>(converter.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture));
}

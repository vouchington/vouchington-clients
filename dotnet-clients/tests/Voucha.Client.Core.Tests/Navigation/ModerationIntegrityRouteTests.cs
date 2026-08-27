using Voucha.Client.Core.ModerationIntegrity;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class ModerationIntegrityRouteTests
{
  [Theory]
  [InlineData("/report-integrity/flags", ModerationIntegrityRouteKind.ReportFlags)]
  [InlineData("/report-integrity/penalties", ModerationIntegrityRouteKind.ReportPenalties)]
  [InlineData("/vote-integrity/flags", ModerationIntegrityRouteKind.VoteFlags)]
  [InlineData("/vote-integrity/penalties", ModerationIntegrityRouteKind.VotePenalties)]
  public void DedicatedRoutesResolve(string path, ModerationIntegrityRouteKind expected)
  {
    Assert.True(ModerationIntegrityRoutes.TryResolve(path, out var actual));
    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData("/reports")]
  [InlineData("/report-integrity")]
  [InlineData("/vote-integrity/flags/flag-1")]
  public void OtherModerationRoutesDoNotResolve(string path) =>
      Assert.False(ModerationIntegrityRoutes.TryResolve(path, out _));
}

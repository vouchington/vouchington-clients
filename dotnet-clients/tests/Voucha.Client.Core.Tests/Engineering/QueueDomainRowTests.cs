using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Engineering;

public sealed class QueueDomainRowTests
{
  [Fact]
  public void ServerDomainAndReasonRemainVerbatimRatherThanMessageKeys()
  {
    var source = new QueueDomain("native.common.retry", true, null, null, "Maintenance <paused>",
        new(4, false), new(1, true), new(2, true), new(8, true), new(0, false));
    var row = new QueueDomainRow(source, UiLocalization.English);

    Assert.Equal("native.common.retry", row.DomainText.VerbatimValue);
    Assert.Null(row.DomainText.Key);
    Assert.Equal("Maintenance <paused>", row.ReasonText.VerbatimValue);
    Assert.Null(row.ReasonText.Key);
    Assert.Equal("native.common.retry", row.DomainPresentation);
    Assert.Equal("Maintenance <paused>", row.ReasonPresentation);
    Assert.Same(source, row.Source);
  }

  [Fact]
  public void AbsentPauseReasonRendersAsEmptyVerbatimText()
  {
    var source = new QueueDomain("running", false, null, null, null,
        new(0, true), new(0, true), new(0, true), new(0, true), new(0, true));
    var row = new QueueDomainRow(source);

    Assert.Equal(string.Empty, row.ReasonText.VerbatimValue);
    Assert.Equal(string.Empty, row.ReasonPresentation);
  }
}

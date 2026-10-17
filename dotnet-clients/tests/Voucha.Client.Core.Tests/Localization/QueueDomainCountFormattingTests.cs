using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Localization;

public sealed class QueueDomainCountFormattingTests
{
  [Theory]
  [InlineData(true, "1,234")]
  [InlineData(false, "1,234+")]
  public void FormatsNumbersForTheCurrentLocaleAndMarksIncompleteCounts(bool complete, string expected)
  {
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var formatter = new UiLocalizedValueFormatter(new UiLocalization(controller));
    Assert.Equal(expected, formatter.Format(new DomainStateCount(1234, complete), "domainCount"));
    controller.ApplySavedLocale("es");
    Assert.Equal(complete ? "1.234" : "1.234+", formatter.Format(new DomainStateCount(1234, complete), "domainCount"));
  }

  [Fact]
  public void RejectsUntypedCounts()
  {
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var formatter = new UiLocalizedValueFormatter(new UiLocalization(controller));
    Assert.Throws<ArgumentException>(() => formatter.Format(1, "domainCount"));
  }
}

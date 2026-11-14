using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class MauiReviewQueueExposureWiringTests
{
  [Fact]
  public void RegistersExposureServiceAndReviewQueueFactory()
  {
    var source = AppSource.Read("MauiProgram.cs");

    Assert.Contains(
        "AddSingleton<IModerationExposureService>",
        source,
        StringComparison.Ordinal);
    Assert.Contains("new ReviewQueueViewModel(", source, StringComparison.Ordinal);
    Assert.Contains(
        "sp.GetRequiredService<IModerationExposureService>()",
        source,
        StringComparison.Ordinal);
    Assert.Contains("sp.GetRequiredService<AppConfig>()", source, StringComparison.Ordinal);
    Assert.Contains("sp.GetRequiredService<IUiLocalization>()", source, StringComparison.Ordinal);
    Assert.Contains("sp.GetRequiredService<IUiLocaleController>()", source, StringComparison.Ordinal);
  }
}

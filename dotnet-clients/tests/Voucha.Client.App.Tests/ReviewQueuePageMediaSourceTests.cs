using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class ReviewQueuePageMediaSourceTests
{
  [Fact]
  public void XamlBindsNativeMediaAndExposureControls()
  {
    var source = AppSource("Pages", "ReviewQueuePage.xaml");

    Assert.Contains("x:DataType=\"moderation:ReviewQueueMediaRow\"", source, StringComparison.Ordinal);
    Assert.Contains("Source=\"{Binding Source}\"", source, StringComparison.Ordinal);
    Assert.Contains("Aspect=\"AspectFit\"", source, StringComparison.Ordinal);
    Assert.Contains("AutomationId=\"review-queue-reveal\"", source, StringComparison.Ordinal);
    Assert.Contains("AutomationId=\"review-queue-check-exposure\"", source, StringComparison.Ordinal);
    Assert.Contains("IsEnabled=\"{Binding CanRevealMedia}\"", source, StringComparison.Ordinal);
    Assert.Contains("IsEnabled=\"{Binding CanAct}\"", source, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding ShowMedia}\"", source, StringComparison.Ordinal);
  }

  [Fact]
  public void CodeBehindRoutesExposureActionsThroughPageOperations()
  {
    var source = AppSource("Pages", "ReviewQueuePage.xaml.cs");

    Assert.Contains("OnRevealClicked", source, StringComparison.Ordinal);
    Assert.Contains("viewModel.RevealMediaAsync", source, StringComparison.Ordinal);
    Assert.Contains("OnCheckExposureClicked", source, StringComparison.Ordinal);
    Assert.Contains("viewModel.RefreshExposureAsync", source, StringComparison.Ordinal);
    Assert.Contains("RunPageOperationAsync", source, StringComparison.Ordinal);
    Assert.Contains("viewModel.CancelExposureOperations()", source, StringComparison.Ordinal);
  }

  private static string AppSource(
      string directory,
      string file,
      [CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src")))
    {
      root = root.Parent;
    }
    return File.ReadAllText(Path.Combine(
        root?.FullName ?? throw new DirectoryNotFoundException(),
        "src",
        "Voucha.Client.App",
        directory,
        file));
  }
}

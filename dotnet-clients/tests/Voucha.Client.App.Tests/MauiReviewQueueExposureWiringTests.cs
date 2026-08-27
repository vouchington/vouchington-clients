using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class MauiReviewQueueExposureWiringTests
{
  [Fact]
  public void RegistersExposureServiceAndReviewQueueFactory()
  {
    var source = AppSource("MauiProgram.cs");

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

  private static string AppSource(
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
        file));
  }
}

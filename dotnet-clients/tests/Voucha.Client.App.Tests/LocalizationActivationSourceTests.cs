using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class LocalizationActivationSourceTests
{
  [Fact]
  public void WindowActivationRevalidatesLocalization()
  {
    var source = AppSource();

    Assert.Contains("window.Activated += OnWindowActivated;", source, StringComparison.Ordinal);
    Assert.Contains("return window;", source, StringComparison.Ordinal);
    Assert.Contains(
        "private void OnWindowActivated(object? sender, EventArgs eventArgs) =>",
        source,
        StringComparison.Ordinal);
    Assert.Contains(
        "_ = serviceProvider.GetRequiredService<LocalizationRefreshService>().RefreshChromeAsync();",
        source,
        StringComparison.Ordinal);
  }

  private static string AppSource([CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src"))) root = root.Parent;
    return File.ReadAllText(Path.Combine(root!.FullName, "src", "Voucha.Client.App", "App.xaml.cs"));
  }
}

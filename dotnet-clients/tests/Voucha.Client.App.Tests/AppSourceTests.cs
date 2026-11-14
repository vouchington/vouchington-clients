using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class AppSourceTests
{
  [Fact]
  public void ReadReturnsAppProjectSource()
  {
    var source = AppSource.Read("MauiProgram.cs");

    Assert.Contains("public static partial class MauiProgram", source, StringComparison.Ordinal);
  }

  [Fact]
  public void LocateUsesTheSourceCheckoutInsteadOfTheRunningTestOutput()
  {
    var root = Path.Combine(Path.GetTempPath(), $"app-source-{Guid.NewGuid():N}");
    var checkout = Path.Combine(root, "checkout");
    var sourceDirectory = Path.Combine(checkout, "dotnet-clients", "tests", "Voucha.Client.App.Tests");
    var appDirectory = Path.Combine(checkout, "dotnet-clients", "src", "Voucha.Client.App");
    try
    {
      Directory.CreateDirectory(sourceDirectory);
      Directory.CreateDirectory(appDirectory);
      var expected = Path.Combine(appDirectory, "MauiProgram.cs");
      File.WriteAllText(expected, "checkout source");

      var actual = AppSource.LocateFromSource(Path.Combine(sourceDirectory, "AppSource.cs"), "MauiProgram.cs");

      Assert.Equal(expected, actual);
      Assert.Equal("checkout source", File.ReadAllText(actual));
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void ReadReportsAMissingAppFile()
  {
    var error = Assert.Throws<DirectoryNotFoundException>(() =>
        AppSource.Read("Missing", "does-not-exist.cs"));

    Assert.Equal("Could not find repository root from test output directory.", error.Message);
  }
}

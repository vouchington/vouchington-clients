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
  public void ReadReportsAMissingAppFile()
  {
    var error = Assert.Throws<DirectoryNotFoundException>(() =>
        AppSource.Read("Missing", "does-not-exist.cs"));

    Assert.Equal("Could not find repository root from test output directory.", error.Message);
  }
}

using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class FilamentsContractPathsTests
{
  [Fact]
  public void ApiFixturesV1RootRequiresExplicitContractRoot()
  {
    var error = Assert.Throws<InvalidOperationException>(() => FilamentsContractPaths.ApiFixturesV1Root(null));

    Assert.Contains("VOUCHA_FILAMENTS_CONTRACT_ROOT", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void ApiFixturesV1RootRejectsDirectoryWithoutFetchedApiFixtures()
  {
    var error = Assert.Throws<DirectoryNotFoundException>(() =>
        FilamentsContractPaths.ApiFixturesV1Root(Path.GetFullPath("dotnet-clients")));

    Assert.Contains("api-fixtures/v1", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void ApiFixturesV1RootDoesNotDiscoverAValidSiblingFilamentsDirectory()
  {
    var temporaryDirectory = TemporaryDirectory();
    try
    {
      Directory.CreateDirectory(Path.Combine(temporaryDirectory, "filaments", "api-fixtures", "v1"));

      var error = Assert.Throws<InvalidOperationException>(() => FilamentsContractPaths.ApiFixturesV1Root(null));

      Assert.Contains("VOUCHA_FILAMENTS_CONTRACT_ROOT", error.Message, StringComparison.Ordinal);
    }
    finally
    {
      Directory.Delete(temporaryDirectory, recursive: true);
    }
  }

  [Theory]
  [InlineData("/tmp/fixture.json")]
  [InlineData("../fixture.json")]
  [InlineData("responses/../../fixture.json")]
  [InlineData("C:\\fixture.json")]
  public void ApiFixtureRejectsPathsThatCanEscapeTheFixtureRoot(string bodyFile)
  {
    var root = FixtureRoot();
    try
    {
      var error = Assert.Throws<InvalidOperationException>(() => FilamentsContractPaths.ApiFixture(root, bodyFile));

      Assert.Contains("Fixture path", error.Message, StringComparison.Ordinal);
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void ApiFixtureAllowsMissingIntermediateDirectoriesForTheReaderToReport()
  {
    var root = FixtureRoot();
    try
    {
      var fixture = FilamentsContractPaths.ApiFixture(root, "responses/missing/fixture.json");

      Assert.Equal(
          Path.Combine(root, "api-fixtures", "v1", "responses", "missing", "fixture.json"),
          fixture);
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void IsStrictDescendantRejectsTheRootAndSiblingPaths()
  {
    var root = TemporaryDirectory();
    try
    {
      var parent = Directory.GetParent(root)!.FullName;
      var sibling = Path.Combine(parent, $"{Path.GetFileName(root)}-sibling");

      Assert.True(FilamentsContractPaths.IsStrictDescendant(root, Path.Combine(root, "fixture.json")));
      Assert.False(FilamentsContractPaths.IsStrictDescendant(root, root));
      Assert.False(FilamentsContractPaths.IsStrictDescendant(root, sibling));
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void ApiFixtureRejectsSymbolicLinkComponentsAndFiles(bool directoryLink)
  {
    var root = FixtureRoot();
    var outside = TemporaryDirectory();
    try
    {
      var fixturesRoot = Path.Combine(root, "api-fixtures", "v1");
      var fixturePath = Path.Combine(fixturesRoot, "responses", "fixture.json");
      var outsideFixture = Path.Combine(outside, "fixture.json");
      File.WriteAllText(outsideFixture, "outside");

      if (directoryLink)
      {
        Directory.CreateSymbolicLink(Path.Combine(fixturesRoot, "responses"), outside);
      }
      else
      {
        Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
        File.CreateSymbolicLink(fixturePath, outsideFixture);
      }

      var error = Assert.Throws<InvalidOperationException>(() => FilamentsContractPaths.ApiFixture(root, "responses/fixture.json"));

      Assert.Contains("symbolic link", error.Message, StringComparison.Ordinal);
    }
    finally
    {
      Directory.Delete(root, recursive: true);
      Directory.Delete(outside, recursive: true);
    }
  }

  [Fact]
  public void ApiFixtureRejectsBrokenSymbolicLinks()
  {
    var root = FixtureRoot();
    try
    {
      var fixturePath = Path.Combine(root, "api-fixtures", "v1", "fixture.json");
      File.CreateSymbolicLink(fixturePath, Path.Combine(root, "missing.json"));

      var error = Assert.Throws<InvalidOperationException>(() =>
          FilamentsContractPaths.ApiFixture(root, "fixture.json"));

      Assert.Contains("symbolic link", error.Message, StringComparison.Ordinal);
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  private static string FixtureRoot()
  {
    var root = TemporaryDirectory();
    Directory.CreateDirectory(Path.Combine(root, "api-fixtures", "v1"));
    return root;
  }

  private static string TemporaryDirectory()
  {
    var path = Path.Combine(Path.GetTempPath(), $"voucha-fixtures-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);
    return path;
  }
}

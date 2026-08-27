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
}

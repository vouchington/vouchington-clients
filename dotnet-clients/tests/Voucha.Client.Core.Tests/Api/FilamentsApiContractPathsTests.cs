using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class FilamentsApiContractPathsTests
{
  [Fact]
  public void RequiresExplicitRootAndContractsDirectory()
  {
    Assert.Throws<InvalidOperationException>(() => FilamentsContractPaths.ApiContractsV1Root(null));
    var root = TemporaryDirectory();
    try
    {
      Directory.CreateDirectory(Path.Combine(root, "api-fixtures", "v1"));
      Assert.Throws<DirectoryNotFoundException>(() => FilamentsContractPaths.ApiContractsV1Root(root));
    }
    finally { Directory.Delete(root, recursive: true); }
  }

  [Theory]
  [InlineData("")]
  [InlineData("/tmp/request-contracts.json")]
  [InlineData("../request-contracts.json")]
  [InlineData("nested/../../request-contracts.json")]
  [InlineData("C:\\request-contracts.json")]
  [InlineData("nested//request-contracts.json")]
  public void RejectsUnsafePaths(string path)
  {
    var root = TemporaryDirectory();
    try
    {
      Directory.CreateDirectory(Path.Combine(root, "api-contracts", "v1"));
      Assert.Throws<InvalidOperationException>(() => FilamentsContractPaths.ApiContract(root, path));
    }
    finally { Directory.Delete(root, recursive: true); }
  }

  [Fact]
  public void ReadsContractWithoutAResponseFixtureDirectory()
  {
    var root = TemporaryDirectory();
    try
    {
      var directory = Path.Combine(root, "api-contracts", "v1");
      Directory.CreateDirectory(directory);
      File.WriteAllText(Path.Combine(directory, "request-contracts.json"), "contract");
      Assert.Equal("contract", File.ReadAllText(FilamentsContractPaths.ApiContract(root, "request-contracts.json")));
      Assert.Equal(Path.Combine(directory, "nested", "missing.json"),
          FilamentsContractPaths.ApiContract(root, "nested/missing.json"));
    }
    finally { Directory.Delete(root, recursive: true); }
  }

  [Theory]
  [InlineData("root")]
  [InlineData("api-contracts")]
  [InlineData("api-contracts/v1")]
  [InlineData("api-contracts/v1/nested")]
  [InlineData("api-contracts/v1/nested/request-contracts.json")]
  public void RejectsSymbolicLinksAtEveryContractPathComponent(string component)
  {
    var temporary = TemporaryDirectory();
    try
    {
      var root = Path.Combine(temporary, "root");
      var outside = Path.Combine(temporary, "outside");
      Directory.CreateDirectory(Path.Combine(root, "api-contracts", "v1", "nested"));
      Directory.CreateDirectory(Path.Combine(outside, "api-contracts", "v1", "nested"));
      File.WriteAllText(Path.Combine(outside, "request-contracts.json"), "outside");
      var linked = component == "root" ? root : Path.Combine(root, component);
      if (component.EndsWith(".json", StringComparison.Ordinal))
        File.CreateSymbolicLink(linked, Path.Combine(outside, "request-contracts.json"));
      else
      {
        Directory.Delete(linked, recursive: true);
        var target = component switch
        {
          "root" => outside,
          "api-contracts" => Path.Combine(outside, "api-contracts"),
          "api-contracts/v1" => Path.Combine(outside, "api-contracts", "v1"),
          _ => Path.Combine(outside, "api-contracts", "v1", "nested")
        };
        Directory.CreateSymbolicLink(linked, target);
      }
      var error = Assert.Throws<InvalidOperationException>(() =>
          FilamentsContractPaths.ApiContract(root, "nested/request-contracts.json"));
      Assert.Contains("symbolic link", error.Message, StringComparison.Ordinal);
    }
    finally { Directory.Delete(temporary, recursive: true); }
  }

  private static string TemporaryDirectory()
  {
    var path = Path.Combine(Path.GetTempPath(), $"voucha-contracts-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);
    return path;
  }
}

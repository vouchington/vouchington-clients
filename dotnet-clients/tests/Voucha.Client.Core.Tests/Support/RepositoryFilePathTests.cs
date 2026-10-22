using Xunit;

namespace Voucha.Client.Core.Tests.Support;

public sealed class RepositoryFilePathTests
{
  [Fact]
  public void FindReturnsTheNearestAncestorFile()
  {
    var root = TemporaryDirectory();
    try
    {
      var nearer = Path.Combine(root, "child", "docs", "note.txt");
      var farther = Path.Combine(root, "docs", "note.txt");
      var start = Path.Combine(root, "child", "start");
      WriteFile(nearer, "near");
      WriteFile(farther, "far");
      Directory.CreateDirectory(start);

      var found = RepositoryFilePath.Find(new DirectoryInfo(start), "docs", "note.txt");

      Assert.Equal(new FileInfo(nearer).FullName, new FileInfo(found).FullName);
      Assert.Equal("near", File.ReadAllText(found));
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void FindReturnsAFileInTheStartDirectory()
  {
    var root = TemporaryDirectory();
    try
    {
      var note = Path.Combine(root, "note.txt");
      WriteFile(note, "here");

      var found = RepositoryFilePath.Find(new DirectoryInfo(root), "note.txt");

      Assert.Equal(new FileInfo(note).FullName, new FileInfo(found).FullName);
      Assert.Equal("here", File.ReadAllText(found));
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void FindSkipsADirectoryAndReportsAMissingFile()
  {
    var root = TemporaryDirectory();
    try
    {
      var start = Path.Combine(root, "child");
      Directory.CreateDirectory(Path.Combine(root, "marker"));
      Directory.CreateDirectory(start);

      var error = Assert.Throws<DirectoryNotFoundException>(() =>
          RepositoryFilePath.Find(new DirectoryInfo(start), "marker"));

      Assert.Equal("Could not find repository root from test output directory.", error.Message);
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  private static string TemporaryDirectory()
  {
    var path = Path.Combine(Path.GetTempPath(), $"voucha-repo-path-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);
    return path;
  }

  private static void WriteFile(string path, string contents)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, contents);
  }
}

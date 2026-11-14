using System.Runtime.CompilerServices;
using Voucha.Client.Core.Tests.Support;

namespace Voucha.Client.App.Tests;

internal static class AppSource
{
  internal static string Read(params string[] relativeParts) =>
      File.ReadAllText(Locate(relativeParts));

  internal static string Locate(params string[] relativeParts) =>
      LocateFromSource(SourceFile(), relativeParts);

  internal static string LocateFromSource(string sourceFile, params string[] relativeParts) =>
      RepositoryFilePath.Find(new DirectoryInfo(Path.GetDirectoryName(sourceFile)!), Prefixed(relativeParts));

  private static string SourceFile([CallerFilePath] string sourceFile = "") => sourceFile;

  private static string[] Prefixed(string[] relativeParts)
  {
    var parts = new List<string> { "dotnet-clients", "src", "Voucha.Client.App" };
    foreach (var part in relativeParts)
    {
      if (!string.IsNullOrEmpty(part)) parts.Add(part);
    }

    return parts.ToArray();
  }
}

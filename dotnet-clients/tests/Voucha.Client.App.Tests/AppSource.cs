using Voucha.Client.Core.Tests.Support;

namespace Voucha.Client.App.Tests;

internal static class AppSource
{
  internal static string Read(params string[] relativeParts) =>
      File.ReadAllText(Locate(relativeParts));

  internal static string Locate(params string[] relativeParts) =>
      RepositoryFilePath.Find(Prefixed(relativeParts));

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

namespace Voucha.Client.Core.Tests.Support;

internal static class RepositoryFilePath
{
  internal static string Find(params string[] relativeParts) =>
      Find(new DirectoryInfo(AppContext.BaseDirectory), relativeParts);

  internal static string Find(DirectoryInfo start, params string[] relativeParts)
  {
    ArgumentNullException.ThrowIfNull(start);
    var directory = start;
    while (directory is not null)
    {
      var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
      if (File.Exists(candidate))
      {
        return candidate;
      }

      directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }
}

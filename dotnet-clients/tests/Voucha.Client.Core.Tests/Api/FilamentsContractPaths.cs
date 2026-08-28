namespace Voucha.Client.Core.Tests.Api;

internal static class FilamentsContractPaths
{
  private const string ContractRootEnvironmentVariable = "VOUCHA_FILAMENTS_CONTRACT_ROOT";

  internal static string ApiFixturesV1Root() =>
      ApiFixturesV1Root(Environment.GetEnvironmentVariable(ContractRootEnvironmentVariable));

  internal static string ApiFixturesV1Root(string? contractRoot)
  {
    if (string.IsNullOrWhiteSpace(contractRoot))
    {
      throw new InvalidOperationException(
          $"{ContractRootEnvironmentVariable} is required to locate fetched Filaments contracts.");
    }

    var root = Path.GetFullPath(contractRoot);
    var apiFixturesRoot = Path.Combine(root, "api-fixtures", "v1");
    if (!Directory.Exists(apiFixturesRoot))
    {
      throw new DirectoryNotFoundException(
          $"{ContractRootEnvironmentVariable} must contain api-fixtures/v1: {root}");
    }

    RejectSymbolicLink(root);
    RejectSymbolicLink(Path.Combine(root, "api-fixtures"));
    RejectSymbolicLink(apiFixturesRoot);
    return apiFixturesRoot;
  }

  internal static string ApiFixture(string relativePath) =>
      ApiFixtureAtRoot(ApiFixturesV1Root(), relativePath);

  internal static string ApiFixture(string contractRoot, string relativePath) =>
      ApiFixtureAtRoot(ApiFixturesV1Root(contractRoot), relativePath);

  private static string ApiFixtureAtRoot(string apiFixturesRoot, string relativePath)
  {
    var segments = FixturePathSegments(relativePath);
    var fixturePath = Path.GetFullPath(Path.Combine([apiFixturesRoot, .. segments]));
    if (!IsStrictDescendant(apiFixturesRoot, fixturePath))
    {
      throw new InvalidOperationException($"Fixture path escapes api-fixtures/v1: {relativePath}");
    }

    var current = apiFixturesRoot;
    foreach (var segment in segments)
    {
      current = Path.Combine(current, segment);
      RejectSymbolicLink(current);
    }

    return fixturePath;
  }

  private static string[] FixturePathSegments(string relativePath)
  {
    if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
    {
      throw new InvalidOperationException($"Fixture path must be relative: {relativePath}");
    }

    var segments = relativePath.Split(['/', '\\'], StringSplitOptions.None);
    if (segments.Any(segment =>
            string.IsNullOrEmpty(segment) || segment is "." or ".." || segment.Contains(':')))
    {
      throw new InvalidOperationException($"Fixture path contains an unsafe segment: {relativePath}");
    }

    return segments;
  }

  internal static bool IsStrictDescendant(string root, string path)
  {
    var normalizedRoot = Path.GetFullPath(root);
    var normalizedPath = Path.GetFullPath(path);
    var rootWithSeparator = Path.EndsInDirectorySeparator(normalizedRoot)
        ? normalizedRoot
        : $"{normalizedRoot}{Path.DirectorySeparatorChar}";
    var comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    return normalizedPath.StartsWith(rootWithSeparator, comparison);
  }

  private static void RejectSymbolicLink(string path)
  {
    if (new FileInfo(path).LinkTarget is not null || new DirectoryInfo(path).LinkTarget is not null)
    {
      throw new InvalidOperationException($"Fixture path contains a symbolic link: {path}");
    }

    try
    {
      if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
      {
        throw new InvalidOperationException($"Fixture path contains a symbolic link: {path}");
      }
    }
    catch (FileNotFoundException)
    {
      // A missing fixture is reported by the caller that reads it.
    }
    catch (DirectoryNotFoundException)
    {
      // A missing fixture is reported by the caller that reads it.
    }
  }
}

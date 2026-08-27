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

    return apiFixturesRoot;
  }

  internal static string ApiFixture(string relativePath) =>
      Path.Combine(ApiFixturesV1Root(), relativePath);
}

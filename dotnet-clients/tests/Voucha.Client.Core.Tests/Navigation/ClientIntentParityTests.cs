using System.Text.Json;
using System.Text.Json.Serialization;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class ClientIntentParityTests
{
  [Fact]
  public void SharedIntentContractMapsToTypedDotNetSections()
  {
    var contract = ClientIntentParityContract.Load();
    var sectionNames = Enum.GetNames<ClientIntentSection>().Select(ToCamelCase).ToHashSet(StringComparer.Ordinal);

    Assert.NotEmpty(contract.Intents);
    foreach (var intent in contract.Intents)
    {
      Assert.Contains(intent.DotnetSection, sectionNames);
    }
  }

  [Fact]
  public void DotNetBottomTabsMirrorSharedBottomNavContractForSignedInUsers()
  {
    var contract = ClientIntentParityContract.Load();
    var expected = contract.Intents
        .Where(intent => string.Equals(intent.NativePlacement, "bottom-nav", StringComparison.Ordinal))
        .Where(intent => intent.FeatureFlag is null)
        .Select(intent => intent.Id);
    var actual = BottomTabShellViewModel.Create(new NavigationViewer(true, [])).Tabs.Select(tab => tab.Id);

    Assert.Equal(expected, actual);
  }

  [Fact]
  public void DotNetVisibleIntentsMirrorSharedContractForAnonymousAndAuthenticatedViewers()
  {
    var contract = ClientIntentParityContract.Load();

    AssertVisibleIntents(contract, NavigationViewer.Anonymous);
    AssertVisibleIntents(contract, new NavigationViewer(true, []));
    AssertVisibleIntents(contract, new NavigationViewer(true, [], new Dictionary<string, bool> { ["fediverse"] = true }));
  }

  [Fact]
  public void DotNetVisibleIntentsMirrorSharedContractForRoleGatedViewers()
  {
    var contract = ClientIntentParityContract.Load();

    AssertVisibleIntents(contract, new NavigationViewer(true, ["administrator"]));
    AssertVisibleIntents(contract, new NavigationViewer(true, ["investor"]));
  }

  private static string ToCamelCase(string value) =>
      char.ToLowerInvariant(value[0]) + value[1..];

  private static void AssertVisibleIntents(ClientIntentParityContract contract, NavigationViewer viewer)
  {
    var expected = contract.Intents
        .Where(intent => IsVisible(intent, viewer))
        .Select(intent => intent.Id)
        .ToArray();
    var actual = NavigationCatalog.GetVisibleIntents(viewer).Select(intent => intent.Id).ToArray();

    Assert.Equal(expected, actual);
    if (viewer.IsAuthenticated && viewer.Roles.Count == 0)
    {
      Assert.Contains("moderation", actual);
    }
  }

  private static bool IsVisible(ClientIntentParityIntent intent, NavigationViewer viewer)
  {
    if (intent.FeatureFlag is not null &&
        (viewer.FeatureFlags?.TryGetValue(intent.FeatureFlag, out var enabled) != true || !enabled))
    {
      return false;
    }

    if (intent.RequiresAuth && !viewer.IsAuthenticated)
    {
      return false;
    }

    return intent.Roles.Count == 0 ||
        viewer.Roles.Any(role => intent.Roles.Contains(role, StringComparer.Ordinal));
  }

  private sealed record ClientIntentParityContract(
      [property: JsonPropertyName("intents")] IReadOnlyList<ClientIntentParityIntent> Intents)
  {
    public static ClientIntentParityContract Load()
    {
      var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
      var path = Path.Combine(repoRoot, "api-fixtures", "v1", "client-intents.json");
      return JsonSerializer.Deserialize<ClientIntentParityContract>(File.ReadAllText(path))
          ?? throw new InvalidOperationException("api-fixtures/v1/client-intents.json is empty.");
    }

    private static string FindRepoRoot(string startDirectory)
    {
      var directory = new DirectoryInfo(startDirectory);
      while (directory is not null)
      {
        if (File.Exists(Path.Combine(directory.FullName, "api-fixtures", "v1", "client-intents.json")))
        {
          return directory.FullName;
        }

        directory = directory.Parent;
      }

      throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
    }
  }

  private sealed record ClientIntentParityIntent(
      [property: JsonPropertyName("id")] string Id,
      [property: JsonPropertyName("label")] string Label,
      [property: JsonPropertyName("nativePlacement")] string NativePlacement,
      [property: JsonPropertyName("dotnetSection")] string DotnetSection,
      [property: JsonPropertyName("featureFlag")] string? FeatureFlag,
      [property: JsonPropertyName("requiresAuth")] bool RequiresAuth,
      [property: JsonPropertyName("roles")] IReadOnlyList<string> Roles);
}

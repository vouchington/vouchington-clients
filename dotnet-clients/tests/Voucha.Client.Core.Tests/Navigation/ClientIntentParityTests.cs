using System.Text.Json;
using System.Text.Json.Serialization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class ClientIntentParityTests
{
  [Fact]
  public void DotNetBottomTabsMirrorSharedBottomNavContractForSignedInUsers()
  {
    var contract = ClientIntentParityContract.Load();
    var actual = BottomTabShellViewModel.Create(new NavigationViewer(true, [])).Tabs.Select(tab => tab.Id).ToArray();
    var expected = contract.Intents
        .Where(intent => string.Equals(intent.NativePlacement, "bottom-nav", StringComparison.Ordinal))
        .Where(intent => intent.FeatureFlag is null)
        .Where(intent => actual.Contains(intent.Id, StringComparer.Ordinal))
        .Select(intent => intent.Id)
        .ToArray();

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

  private static void AssertVisibleIntents(ClientIntentParityContract contract, NavigationViewer viewer)
  {
    var expected = contract.Intents
        .Where(intent => IsVisible(intent, viewer))
        .Where(intent => NavigationCatalog.All.Any(nativeIntent =>
            string.Equals(nativeIntent.Id, intent.Id, StringComparison.Ordinal)))
        .Select(intent => intent.Id)
        .ToArray();
    var actual = NavigationCatalog.GetVisibleIntents(viewer)
        .Where(nativeIntent => contract.Intents.Any(intent =>
            string.Equals(intent.Id, nativeIntent.Id, StringComparison.Ordinal)))
        .Select(intent => intent.Id)
        .ToArray();

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
      var path = FilamentsContractPaths.ApiFixture("client-intents.json");
      return JsonSerializer.Deserialize<ClientIntentParityContract>(File.ReadAllText(path))
          ?? throw new InvalidOperationException("api-fixtures/v1/client-intents.json is empty.");
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

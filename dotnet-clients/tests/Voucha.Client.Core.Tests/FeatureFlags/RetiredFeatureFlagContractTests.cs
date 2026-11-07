using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.FeatureFlags;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.FeatureFlags;

public sealed class RetiredFeatureFlagContractTests
{
  private static readonly string[] RetiredFlags =
  [
    "membershipAppleBilling", "membershipGoogleBilling", "membershipMicrosoftBilling",
    "chat", "support", "combinedSearch",
  ];

  [Fact]
  public async Task CurrentPublicFixtureHidesPersistedRetiredOverridesWithoutGatingChat()
  {
    var token = TestContext.Current.CancellationToken;
    var directory = Path.Combine(Path.GetTempPath(), $"voucha-retired-flags-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
      var path = Path.Combine(directory, "overrides.json");
      var stale = RetiredFlags.ToDictionary(key => key, _ => true);
      stale["fediverse"] = true;
      await File.WriteAllTextAsync(path, JsonSerializer.Serialize(stale), token);
      using var store = new FileFeatureFlagOverrideStore(path);
      using var state = new FeatureFlagState(store);
      await state.InitializeAsync(token);
      Assert.Empty(state.EffectiveFlags);
      using var http = new HttpClient(new FixtureHandler()) { BaseAddress = new Uri("https://api.test") };
      var session = new SessionStore();
      using var model = new FeatureFlagOverridesViewModel(
          new ApiFeatureFlagService(new VouchaApiClient(http)), state, session);

      await model.LoadAsync(token);

      Assert.False(model.HasError);
      Assert.Equal(["fediverse", "memberships"], model.Flags.Select(flag => flag.Key));
      Assert.Equal(["fediverse", "memberships"], state.EffectiveFlags.Keys.Order());
      Assert.False(state.RemoteFlags["fediverse"]);
      Assert.True(state.EffectiveFlags["fediverse"]);
      foreach (var retired in RetiredFlags)
      {
        Assert.DoesNotContain(retired, state.EffectiveFlags);
        Assert.False(await model.SetOverrideAsync(retired, false, token));
      }
      var viewer = NavigationCatalog.FromIdentity(session.Current.Identity, state.EffectiveFlags);
      Assert.Contains(NavigationCatalog.GetVisibleBottomTabs(viewer), intent => intent.Id == "chat");
      Assert.True(await model.ClearOverridesAsync(token));
      Assert.Empty(await store.LoadAsync(token));
      Assert.False(state.EffectiveFlags["fediverse"]);
    }
    finally
    {
      Directory.Delete(directory, recursive: true);
    }
  }

  [Theory]
  [InlineData("native.dynamic-config.update.changed", true)]
  [InlineData("native.dynamic-config.update.no-op", false)]
  public void CurrentUpdateFixturesBuildOnlyActiveBooleanEditors(string fixtureId, bool changed)
  {
    var response = JsonSerializer.Deserialize<DynamicConfigUpdateResponse>(
        ApiFixtureLoader.LoadResponse(fixtureId), VouchaApiJson.Options)!;
    Assert.Equal(changed, response.Changed);
    Assert.Equal("feature-flags", response.Namespace.Namespace);
    Assert.Equal(2, response.Namespace.FieldCount);
    Assert.Equal(["fediverse", "memberships"], response.Namespace.Config.Keys.Order());
    Assert.Equal(["fediverse", "memberships"], response.Namespace.Fields.Select(field => field.Name).Order());
    foreach (var field in response.Namespace.Fields)
    {
      var editor = new DynamicConfigFieldViewModel(field);
      Assert.True(editor.IsBoolean);
      Assert.False(editor.HasValidationError);
      Assert.True(editor.TryValue(out var value));
      Assert.Equal(response.Namespace.Config[field.Name], value);
      Assert.False(Assert.IsType<DynamicConfigBooleanValue>(field.DefaultValue).Value);
      Assert.Equal(field.Name == "fediverse" && changed, editor.BooleanValue);
    }
    var roundTrip = JsonSerializer.Deserialize<DynamicConfigUpdateResponse>(
        JsonSerializer.Serialize(response, VouchaApiJson.Options), VouchaApiJson.Options)!;
    Assert.Equal(response.Namespace.Config, roundTrip.Namespace.Config);
  }

  private sealed class FixtureHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Assert.Equal("/api/v1/feature-flags", request.RequestUri!.AbsolutePath);
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(ApiFixtureLoader.LoadResponse("native.feature-flags.default")),
      });
    }
  }

  private sealed class SessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User("developer-1", "developer", Roles: ["developer"]));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

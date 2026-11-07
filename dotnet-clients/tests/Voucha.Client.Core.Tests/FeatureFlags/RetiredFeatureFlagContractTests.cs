using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
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

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CurrentPublicFixtureHidesPersistedRetiredOverridesWithoutGatingChat(bool includeStripe)
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
      var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.feature-flags.default"))!;
      if (includeStripe) body["flags"]!["membershipStripeBilling"] = true;
      var response = JsonSerializer.Deserialize<FeatureFlagsResponse>(body.ToJsonString(), VouchaApiJson.Options)!;
      using var http = new HttpClient(new FixtureHandler(body.ToJsonString())) { BaseAddress = new Uri("https://api.test") };
      var session = new SessionStore();
      using var model = new FeatureFlagOverridesViewModel(
          new ApiFeatureFlagService(new VouchaApiClient(http)), state, session);

      await model.LoadAsync(token);

      Assert.False(model.HasError);
      Assert.Equal(response.Flags.Keys.Order(StringComparer.Ordinal), model.Flags.Select(flag => flag.Key));
      Assert.Equal(response.Flags.Keys.Order(StringComparer.Ordinal), state.EffectiveFlags.Keys.Order(StringComparer.Ordinal));
      Assert.Contains("memberships", state.RemoteFlags);
      Assert.Contains("fediverse", state.RemoteFlags);
      if (includeStripe)
      {
        Assert.True(state.EffectiveFlags["membershipStripeBilling"]);
        Assert.True(await model.SetOverrideAsync("membershipStripeBilling", false, token));
        Assert.False(state.EffectiveFlags["membershipStripeBilling"]);
      }
      Assert.False(state.RemoteFlags["fediverse"]);
      Assert.True(state.EffectiveFlags["fediverse"]);
      foreach (var retired in RetiredFlags)
      {
        Assert.DoesNotContain(retired, state.RemoteFlags);
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
  [InlineData("native.dynamic-config.update.changed", true, false)]
  [InlineData("native.dynamic-config.update.no-op", false, false)]
  [InlineData("native.dynamic-config.update.changed", true, true)]
  [InlineData("native.dynamic-config.update.no-op", false, true)]
  public void CurrentUpdateFixturesBuildOnlyActiveBooleanEditors(string fixtureId, bool changed, bool includeStripe)
  {
    var response = JsonSerializer.Deserialize<DynamicConfigUpdateResponse>(
        ApiFixtureLoader.LoadResponse(fixtureId), VouchaApiJson.Options)!;
    if (includeStripe)
    {
      var config = response.Namespace.Config.ToDictionary();
      config["membershipStripeBilling"] = DynamicConfigValues.From(true);
      var stripe = new DynamicConfigField("membershipStripeBilling", "boolean", "",
          DynamicConfigValues.From(true), DynamicConfigValues.From(false));
      var fields = response.Namespace.Fields.Where(field => field.Name != stripe.Name).Append(stripe).ToArray();
      response = response with
      {
        Namespace = response.Namespace with
        {
          Config = config,
          Fields = fields,
          FieldCount = fields.Length,
        }
      };
    }
    response = JsonSerializer.Deserialize<DynamicConfigUpdateResponse>(
        JsonSerializer.Serialize(response, VouchaApiJson.Options), VouchaApiJson.Options)!;
    Assert.Equal(changed, response.Changed);
    Assert.Equal("feature-flags", response.Namespace.Namespace);
    Assert.Equal(response.Namespace.Fields.Count, response.Namespace.FieldCount);
    Assert.Equal(response.Namespace.Config.Keys.Order(StringComparer.Ordinal), response.Namespace.Fields.Select(field => field.Name).Order(StringComparer.Ordinal));
    Assert.Contains("memberships", response.Namespace.Config);
    Assert.Contains("fediverse", response.Namespace.Config);
    foreach (var retired in RetiredFlags) Assert.DoesNotContain(retired, response.Namespace.Config);
    foreach (var field in response.Namespace.Fields)
    {
      var editor = new DynamicConfigFieldViewModel(field);
      Assert.True(editor.IsBoolean);
      Assert.False(editor.HasValidationError);
      Assert.True(editor.TryValue(out var value));
      Assert.Equal(response.Namespace.Config[field.Name], value);
      Assert.False(Assert.IsType<DynamicConfigBooleanValue>(field.DefaultValue).Value);
      Assert.Equal(Assert.IsType<DynamicConfigBooleanValue>(response.Namespace.Config[field.Name]).Value, editor.BooleanValue);
    }
    if (includeStripe)
    {
      var stripe = new DynamicConfigFieldViewModel(response.Namespace.Fields.Single(field => field.Name == "membershipStripeBilling"));
      Assert.True(stripe.BooleanValue);
    }
  }

  private sealed class FixtureHandler(string body) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Assert.Equal("/api/v1/feature-flags", request.RequestUri!.AbsolutePath);
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(body),
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

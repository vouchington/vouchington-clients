#if DEBUG
using System.Text;
using System.Text.Json.Nodes;
using Voucha.Client.App;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class CatalystGuiAuditTests : IDisposable
{
  private readonly string directory = Path.Combine(Path.GetTempPath(), "voucha-gui-audit-" + Guid.NewGuid());
  private string ResultPath => Path.Combine(directory, "result.json");
  private string AuditPath => ResultPath + ".app.json";

  public CatalystGuiAuditTests() => Directory.CreateDirectory(directory);

  [Fact]
  public async Task HandlerRecordsActualOneFieldPatchesAndPreservesHiddenPreferences()
  {
    var preferences = Fixture();
    var original = preferences["email_preferences"]!.DeepClone();
    var audit = CatalystGuiAudit.Create(true, ResultPath, original.AsObject());
    using var client = Client(preferences, audit);

    using var response = await client.PatchAsync("/api/v1/my/email-preferences", Json("""{"is_engagement_emails_enabled":false}"""), TestContext.Current.CancellationToken);
    response.EnsureSuccessStatusCode();
    using var get = await client.GetAsync("/api/v1/my/email-preferences", TestContext.Current.CancellationToken);
    var persisted = JsonNode.Parse(await get.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!["email_preferences"]!;
    var expected = original.DeepClone().AsObject();
    expected["is_engagement_emails_enabled"] = false;
    Assert.True(JsonNode.DeepEquals(expected, persisted));
    var report = Report();
    Assert.Equal(2, report["schemaVersion"]!.GetValue<int>());
    Assert.True(JsonNode.DeepEquals(original, report["initialPreferences"]));
    var patch = Assert.IsType<JsonObject>(Assert.Single(report["patches"]!.AsArray()));
    Assert.Equal(["is_engagement_emails_enabled"], patch["fields"]!.AsArray().Select(field => field!.GetValue<string>()));
    Assert.True(JsonNode.DeepEquals(original, patch["before"]));
    Assert.True(JsonNode.DeepEquals(persisted, patch["after"]));
    Assert.False(File.Exists(AuditPath + ".tmp"));
  }

  [Theory]
  [InlineData("not json")]
  [InlineData("[]")]
  [InlineData("null")]
  public async Task InvalidPatchesDoNotInventPersistenceEvidence(string body)
  {
    var preferences = Fixture();
    var original = preferences.DeepClone();
    var audit = CatalystGuiAudit.Create(true, ResultPath, preferences["email_preferences"]!.AsObject());
    using var client = Client(preferences, audit);

    using var response = await client.PatchAsync("/api/v1/my/email-preferences", Json(body), TestContext.Current.CancellationToken);

    Assert.True(JsonNode.DeepEquals(original, JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))));
    Assert.Empty(Report()["patches"]!.AsArray());
  }

  [Fact]
  public async Task ConcurrentPatchesHaveCoherentBeforeAndAfterSnapshots()
  {
    var preferences = Fixture();
    var original = preferences["email_preferences"]!.DeepClone();
    var audit = CatalystGuiAudit.Create(true, ResultPath, original.AsObject());
    using var client = Client(preferences, audit);
    await Task.WhenAll(Enumerable.Range(0, 12).Select(index => Task.Run(async () =>
    {
      using var response = await client.PatchAsync("/api/v1/my/email-preferences", Json($$"""{"is_engagement_emails_enabled":{{(index % 2 == 0 ? "true" : "false")}}}"""), TestContext.Current.CancellationToken);
      response.EnsureSuccessStatusCode();
    }, TestContext.Current.CancellationToken)));

    var patches = Report()["patches"]!.AsArray();
    Assert.Equal(12, patches.Count);
    var prior = original;
    foreach (var node in patches)
    {
      var patch = Assert.IsType<JsonObject>(node);
      Assert.True(JsonNode.DeepEquals(prior, patch["before"]));
      var expected = prior.DeepClone().AsObject();
      expected["is_engagement_emails_enabled"] = patch["after"]!["is_engagement_emails_enabled"]!.DeepClone();
      Assert.True(JsonNode.DeepEquals(expected, patch["after"]));
      prior = patch["after"]!;
    }
  }

  [Fact]
  public void DispatchesBelongToActualActivationsAndRetainLocalizedText()
  {
    var audit = CatalystGuiAudit.Create(true, ResultPath, Fixture()["email_preferences"]!.AsObject())!;
    audit.Activate("/my/notification-settings");
    audit.Focus("notification-preferences-heading", false);
    audit.Focus("notification-preferences-heading", true);
    audit.Announce("Paramètres de notification ouverts");
    audit.Activate("/my/notification-settings");
    audit.Focus("notification-preferences-heading", true);
    audit.Announce("Notification settings opened");

    var report = Report();
    Assert.Equal([1, 2], report["activations"]!.AsArray().Select(value => value!["activationId"]!.GetValue<int>()));
    var dispatches = report["accessibilityDispatches"]!.AsArray();
    Assert.Equal([1, 1, 1, 2, 2], dispatches.Select(value => value!["activationId"]!.GetValue<int>()));
    Assert.False(dispatches[0]!["succeeded"]!.GetValue<bool>());
    Assert.True(dispatches[1]!["succeeded"]!.GetValue<bool>());
    Assert.Equal("Paramètres de notification ouverts", dispatches[2]!["text"]!.GetValue<string>());
    Assert.Equal("announcement", dispatches[4]!["kind"]!.GetValue<string>());
  }

  [Fact]
  public void DepartureAdapterPersistsRouteStateAndAllowsReturnActivation()
  {
    var audit = CatalystGuiAudit.Create(true, ResultPath, new JsonObject())!;
    Assert.False(Report()["notificationRouteActive"]!.GetValue<bool>());
    audit.Activate("/my/notification-settings");
    Assert.True(Report()["notificationRouteActive"]!.GetValue<bool>());

    audit.Deactivate();
    Assert.False(Report()["notificationRouteActive"]!.GetValue<bool>());
    Assert.Single(Report()["activations"]!.AsArray());
    audit.Activate("/my/notification-settings");
    Assert.True(Report()["notificationRouteActive"]!.GetValue<bool>());
    Assert.Equal(2, Report()["activations"]!.AsArray().Count);
  }

  [Fact]
  public void NonFixtureAndUnconfiguredRunsDoNotWriteAudit()
  {
    Assert.Null(CatalystGuiAudit.Create(false, ResultPath, new JsonObject()));
    Assert.Null(CatalystGuiAudit.Create(true, null, new JsonObject()));
    Assert.False(File.Exists(AuditPath));
  }

  [Fact]
  public void InvalidAuditDestinationsFailRatherThanClaimingEvidence()
  {
    Assert.Throws<ArgumentException>(() => CatalystGuiAudit.Create(true, "relative.json", new JsonObject()));
    Assert.Throws<DirectoryNotFoundException>(() => CatalystGuiAudit.Create(true, Path.Combine(directory, "missing", "result.json"), new JsonObject()));
  }

  [Fact]
  public async Task AuditWriteFailureIsNotReturnedAsSuccessfulPersistence()
  {
    var preferences = Fixture();
    var original = preferences.DeepClone();
    var audit = CatalystGuiAudit.Create(true, ResultPath, preferences["email_preferences"]!.AsObject());
    using var client = Client(preferences, audit);
    Directory.Delete(directory, recursive: true);

    await Assert.ThrowsAsync<DirectoryNotFoundException>(() => client.PatchAsync("/api/v1/my/email-preferences", Json("""{"is_engagement_emails_enabled":false}"""), TestContext.Current.CancellationToken));
    Assert.False(File.Exists(AuditPath));
    using var get = await client.GetAsync("/api/v1/my/email-preferences", TestContext.Current.CancellationToken);
    Assert.True(JsonNode.DeepEquals(original, JsonNode.Parse(await get.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))));
    Directory.CreateDirectory(directory);
    using var retry = await client.PatchAsync("/api/v1/my/email-preferences", Json("""{"is_engagement_emails_enabled":false}"""), TestContext.Current.CancellationToken);
    retry.EnsureSuccessStatusCode();
    Assert.Single(Report()["patches"]!.AsArray());
  }

  private static JsonObject Fixture() => JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.my.email-preferences.default"))!.AsObject();
  private JsonObject Report() => JsonNode.Parse(File.ReadAllText(AuditPath))!.AsObject();
  private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
  private static HttpClient Client(JsonObject preferences, CatalystGuiAudit? audit) => new(
      new CatalystGuiFixtureHandler(new RejectNetworkHandler(), "{}", preferences, audit)) { BaseAddress = new Uri("https://voucha.test") };
  public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }

  private sealed class RejectNetworkHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A fixture request must not reach the network.");
  }
}
#endif

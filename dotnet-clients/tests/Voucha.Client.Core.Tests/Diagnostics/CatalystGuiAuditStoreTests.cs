using System.Text.Json.Nodes;
using Voucha.Client.Core.Diagnostics;
using Xunit;

namespace Voucha.Client.Core.Tests.Diagnostics;

public sealed class CatalystGuiAuditStoreTests : IDisposable
{
  private readonly string directory = Path.Combine(Path.GetTempPath(), "voucha-audit-store-" + Guid.NewGuid());
  private string ResultPath => Path.Combine(directory, "result.json");
  private string AuditPath => ResultPath + ".app.json";

  public CatalystGuiAuditStoreTests() => Directory.CreateDirectory(directory);

  [Fact]
  public void PersistsIndependentSnapshotsAndReplacesCompleteReport()
  {
    var before = new JsonObject { ["engagement"] = true, ["hidden"] = "preserve" };
    var initial = before.DeepClone();
    var store = CatalystGuiAuditStore.Create(true, ResultPath, before)!;
    before["engagement"] = false;
    var after = new JsonObject { ["engagement"] = false, ["hidden"] = "preserve" };
    store.Patch(new JsonObject { ["engagement"] = false }, initial.AsObject(), after);
    after["hidden"] = "mutated";
    store.Activate("/my/notification-settings");

    var report = Report();
    Assert.Equal(2, report["schemaVersion"]!.GetValue<int>());
    Assert.True(JsonNode.DeepEquals(initial, report["initialPreferences"]));
    var patch = Assert.IsType<JsonObject>(Assert.Single(report["patches"]!.AsArray()));
    Assert.Equal("engagement", Assert.Single(patch["fields"]!.AsArray())!.GetValue<string>());
    Assert.True(JsonNode.DeepEquals(initial, patch["before"]));
    Assert.False(patch["after"]!["engagement"]!.GetValue<bool>());
    Assert.Equal("preserve", patch["after"]!["hidden"]!.GetValue<string>());
    Assert.Empty(report["accessibilityDispatches"]!.AsArray());
    Assert.False(File.Exists(AuditPath + ".tmp"));
  }

  [Fact]
  public void RouteDeparturePersistsWithoutInventingAnotherActivation()
  {
    var store = CatalystGuiAuditStore.Create(true, ResultPath, new JsonObject())!;
    Assert.False(Report()["notificationRouteActive"]!.GetValue<bool>());
    store.Activate("/my/notification-settings");
    Assert.True(Report()["notificationRouteActive"]!.GetValue<bool>());

    store.Deactivate();
    var departed = File.ReadAllText(AuditPath);
    Assert.False(Report()["notificationRouteActive"]!.GetValue<bool>());
    Assert.Single(Report()["activations"]!.AsArray());
    store.Deactivate();
    Assert.Equal(departed, File.ReadAllText(AuditPath));

    store.Activate("/my/notification-settings");
    var returned = Report();
    Assert.True(returned["notificationRouteActive"]!.GetValue<bool>());
    Assert.Equal([1, 2], returned["activations"]!.AsArray().Select(value => value!["activationId"]!.GetValue<int>()));
    Assert.Empty(returned["accessibilityDispatches"]!.AsArray());
    Assert.False(File.Exists(AuditPath + ".tmp"));
  }

  [Fact]
  public void FailedDepartureCanBeRetriedWithoutLosingPersistedEvidence()
  {
    var store = CatalystGuiAuditStore.Create(true, ResultPath, new JsonObject())!;
    store.Activate("/my/notification-settings");
    Directory.Delete(directory, recursive: true);
    Assert.Throws<DirectoryNotFoundException>(() => store.Deactivate());
    Directory.CreateDirectory(directory);

    store.Deactivate();
    Assert.False(Report()["notificationRouteActive"]!.GetValue<bool>());
    Assert.Single(Report()["activations"]!.AsArray());
  }

  [Fact]
  public void DispatchesRetainActivationAndLocalizedPayload()
  {
    var store = CatalystGuiAuditStore.Create(true, ResultPath, new JsonObject())!;
    store.Dispatch(new JsonObject { ["kind"] = "focus", ["automationId"] = null, ["succeeded"] = false });
    store.Activate("/first");
    store.Dispatch(new JsonObject { ["kind"] = "announcement", ["text"] = "Paramètres ouverts" });
    store.Activate("/second");
    store.Dispatch(new JsonObject { ["kind"] = "focus", ["automationId"] = "heading", ["succeeded"] = true });

    var report = Report();
    var activations = report["activations"]!.AsArray();
    Assert.Equal([1, 2], activations.Select(value => value!["activationId"]!.GetValue<int>()));
    Assert.Equal(["/first", "/second"], activations.Select(value => value!["canonicalPath"]!.GetValue<string>()));
    var dispatches = report["accessibilityDispatches"]!.AsArray();
    Assert.Equal([0, 1, 2], dispatches.Select(value => value!["activationId"]!.GetValue<int>()));
    Assert.Null(dispatches[0]!["automationId"]);
    Assert.False(dispatches[0]!["succeeded"]!.GetValue<bool>());
    Assert.Equal("Paramètres ouverts", dispatches[1]!["text"]!.GetValue<string>());
    Assert.True(dispatches[2]!["succeeded"]!.GetValue<bool>());
  }

  [Theory]
  [InlineData(false, "result.json")]
  [InlineData(true, null)]
  [InlineData(true, "")]
  public void DisabledOrUnconfiguredRunsDoNotCreateFiles(bool fixtureMode, string? resultPath)
  {
    Assert.Null(CatalystGuiAuditStore.Create(fixtureMode, resultPath, new JsonObject()));
    Assert.Empty(Directory.GetFiles(directory));
  }

  [Fact]
  public void InvalidDestinationsFailWithoutClaimingEvidence()
  {
    Assert.Throws<ArgumentException>(() => CatalystGuiAuditStore.Create(true, "relative.json", new JsonObject()));
    Assert.Throws<DirectoryNotFoundException>(() => CatalystGuiAuditStore.Create(true, Path.Combine(directory, "missing", "result.json"), new JsonObject()));
    Assert.Empty(Directory.GetFiles(directory));
  }

  [Fact]
  public void MissingInitialPreferencesDoesNotCreateReport()
  {
    var error = Assert.Throws<ArgumentNullException>(() => CatalystGuiAuditStore.Create(true, ResultPath, null!));
    Assert.Equal("preferences", error.ParamName);
    Assert.Empty(Directory.GetFiles(directory));
  }

  [Theory]
  [InlineData("patch")]
  [InlineData("before")]
  [InlineData("after")]
  [InlineData("canonicalPath")]
  [InlineData("entry")]
  public void NullMutationInputsDoNotChangeReport(string parameter)
  {
    var store = CatalystGuiAuditStore.Create(true, ResultPath, new JsonObject())!;
    var original = File.ReadAllText(AuditPath);
    var error = Assert.Throws<ArgumentNullException>(() =>
    {
      switch (parameter)
      {
        case "patch": store.Patch(null!, new JsonObject(), new JsonObject()); break;
        case "before": store.Patch(new JsonObject(), null!, new JsonObject()); break;
        case "after": store.Patch(new JsonObject(), new JsonObject(), null!); break;
        case "canonicalPath": store.Activate(null!); break;
        case "entry": store.Dispatch(null!); break;
      }
    });
    Assert.Equal(parameter, error.ParamName);
    Assert.Equal(original, File.ReadAllText(AuditPath));
    Assert.False(File.Exists(AuditPath + ".tmp"));
    store.Activate("/valid");
    var report = Report();
    Assert.Equal(1, Assert.Single(report["activations"]!.AsArray())!["activationId"]!.GetValue<int>());
    Assert.Empty(report["patches"]!.AsArray());
    Assert.Empty(report["accessibilityDispatches"]!.AsArray());
  }

  [Fact]
  public void FailedPatchIsRemovedBeforeNextSuccessfulWrite()
  {
    var store = CatalystGuiAuditStore.Create(true, ResultPath, new JsonObject())!;
    Directory.Delete(directory, recursive: true);
    Assert.Throws<DirectoryNotFoundException>(() => store.Patch(
        new JsonObject { ["failed"] = true }, new JsonObject(), new JsonObject { ["failed"] = true }));
    Directory.CreateDirectory(directory);
    store.Patch(new JsonObject { ["saved"] = true }, new JsonObject(), new JsonObject { ["saved"] = true });

    var patch = Assert.Single(Report()["patches"]!.AsArray())!;
    Assert.Equal("saved", Assert.Single(patch["fields"]!.AsArray())!.GetValue<string>());
    Assert.True(patch["after"]!["saved"]!.GetValue<bool>());
    Assert.False(File.Exists(AuditPath + ".tmp"));
  }

  [Fact]
  public void FailedReplacementCleansTemporaryReport()
  {
    var store = CatalystGuiAuditStore.Create(true, ResultPath, new JsonObject())!;
    File.Delete(AuditPath);
    Directory.CreateDirectory(AuditPath);
    Assert.Throws<IOException>(() => store.Patch(new JsonObject(), new JsonObject(), new JsonObject()));
    Assert.False(File.Exists(AuditPath + ".tmp"));
    Directory.Delete(AuditPath);
    store.Activate("/recovered");
    Assert.Empty(Report()["patches"]!.AsArray());
  }

  [Fact]
  public async Task ConcurrentActivationsAndDispatchesProduceCompleteReport()
  {
    var store = CatalystGuiAuditStore.Create(true, ResultPath, new JsonObject())!;
    await Task.WhenAll(Enumerable.Range(0, 12).Select(index => Task.Run(() =>
    {
      store.Activate("/route/" + index);
      store.Dispatch(new JsonObject { ["kind"] = "announcement", ["text"] = index.ToString(System.Globalization.CultureInfo.InvariantCulture) });
    }, TestContext.Current.CancellationToken)));

    var report = Report();
    Assert.Equal(Enumerable.Range(1, 12), report["activations"]!.AsArray().Select(value => value!["activationId"]!.GetValue<int>()));
    Assert.Equal(12, report["accessibilityDispatches"]!.AsArray().Count);
    Assert.All(report["accessibilityDispatches"]!.AsArray(), value => Assert.InRange(value!["activationId"]!.GetValue<int>(), 1, 12));
  }

  private JsonObject Report() => JsonNode.Parse(File.ReadAllText(AuditPath))!.AsObject();
  public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}

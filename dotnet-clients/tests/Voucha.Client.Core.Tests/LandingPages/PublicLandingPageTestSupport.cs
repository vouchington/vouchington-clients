using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Analytics;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Tests.Api;

namespace Voucha.Client.Core.Tests.LandingPages;

internal static class PublicLandingPageTestData
{
  // Unit wrapper only: the published fixture supplies owner item data, not a public response.
  public static string PublicUnitJson()
  {
    var owner = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.landing-page-detail.default"))!;
    return new JsonObject
    {
      ["user"] = new JsonObject { ["id"] = "user-abc", ["username"] = "alice", ["display_name"] = "Alice" },
      ["landing_page"] = owner["landing_page"]!.DeepClone(),
    }.ToJsonString();
  }

  public static PublicLandingPageResponse Response() =>
      JsonSerializer.Deserialize<PublicLandingPageResponse>(PublicUnitJson(), VouchaApiJson.Options)!;
}

internal sealed class PublicLandingSession : ISessionStore
{
  public event EventHandler<SessionChangedEventArgs>? SessionChanged;
  public EventHandler<SessionChangedEventArgs>? CaptureChanges() => SessionChanged;
  public SessionSnapshot Current { get; private set; } = new(new User("viewer-1", "viewer"));
  public void Set(string? id)
  {
    Current = id is null ? SessionSnapshot.Anonymous : new(new User(id, "viewer"));
    SessionChanged?.Invoke(this, new(Current));
  }
  public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  public Task SignOutAsync(CancellationToken cancellationToken = default) { Set(null); return Task.CompletedTask; }
}

internal sealed class PublicLandingSignals : IProductSurfaceAnalytics
{
  public List<string> Visits { get; } = [];
  public List<(string Page, LandingPageClickSignal Signal)> Clicks { get; } = [];
  public void RecordLandingPageVisit(string landingPageId) => Visits.Add(landingPageId);
  public void RecordLandingPageClick(string landingPageId, LandingPageClickSignal signal) => Clicks.Add((landingPageId, signal));
  public void RecordRssFeedView(string rssFeedId) => throw new InvalidOperationException();
  public void RecordRssFeedItemView(string itemId) => throw new InvalidOperationException();
}

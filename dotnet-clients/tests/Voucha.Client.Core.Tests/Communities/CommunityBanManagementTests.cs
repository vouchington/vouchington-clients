using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityBanManagementTests
{
  [Fact]
  public async Task IndividualLiftPreservesSiblingAndDetailLoadsOnDemand()
  {
    var handler = new BanHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var model = new CommunityDetailViewModel(new ApiCommunitiesService(new VouchaApiClient(http)));
    await model.LoadActiveBansAsync("user-1", TestContext.Current.CancellationToken);
    Assert.Equal(2, model.BanRows.Count);
    Assert.Null(model.SelectedBanDetail);
    var first = model.BanRows[0].Id;
    Assert.True(await model.LoadBanDetailAsync(first, TestContext.Current.CancellationToken));
    Assert.Equal("Repeated spam", model.SelectedBanDetail!.Reason);
    Assert.True(await model.LiftBanAsync(first, TestContext.Current.CancellationToken));
    Assert.Single(model.BanRows);
    Assert.Equal("sibling-ban", model.BanRows[0].Id);
    Assert.EndsWith($"/bans/{first}", handler.Paths[^1], StringComparison.Ordinal);
  }

  [Fact]
  public async Task BulkLiftRequiresConfirmationAndUsesReturnedIds()
  {
    var handler = new BanHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var model = new CommunityDetailViewModel(new ApiCommunitiesService(new VouchaApiClient(http)));
    await model.LoadActiveBansAsync("user-1", TestContext.Current.CancellationToken);
    model.RequestLiftAllBans(model.BanRows[0].Id);
    Assert.False(await model.ConfirmLiftAllBansAsync(false, TestContext.Current.CancellationToken));
    Assert.Single(handler.Paths);
    Assert.False(await model.ConfirmLiftAllBansAsync(true, TestContext.Current.CancellationToken));
    model.RequestLiftAllBans(model.BanRows[0].Id);
    Assert.True(await model.ConfirmLiftAllBansAsync(true, TestContext.Current.CancellationToken));
    Assert.Equal("sibling-ban", Assert.Single(model.BanRows).Id);
    Assert.Contains("/bans/users/", handler.Paths[^1], StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("forbidden", 403)]
  [InlineData("overflow", 409)]
  public async Task BulkErrorsUseLocalizedStableCodes(string fixture, int status)
  {
    var handler = new BanHandler { Failure = fixture, Status = (HttpStatusCode)status };
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var model = new CommunityDetailViewModel(new ApiCommunitiesService(new VouchaApiClient(http)));
    await model.LoadActiveBansAsync("user-1", TestContext.Current.CancellationToken);
    model.RequestLiftAllBans(model.BanRows[0].Id);
    Assert.False(await model.ConfirmLiftAllBansAsync(true, TestContext.Current.CancellationToken));
    Assert.Equal(2, model.BanRows.Count);
    Assert.NotNull(model.ErrorMessage);
    Assert.DoesNotContain("COMMUNITY_BAN_", model.ErrorMessage, StringComparison.Ordinal);
    if (fixture == "overflow") Assert.Contains("1", model.ErrorMessage, StringComparison.Ordinal);
  }

  [Fact]
  public async Task ActiveAndHistoryPagesForwardOpaqueCursorAndDeduplicate()
  {
    var handler = new BanHandler { Paged = true };
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var model = new CommunityDetailViewModel(new ApiCommunitiesService(new VouchaApiClient(http)));
    await model.SelectSectionAsync(CommunityDetailSurfaceSection.Bans, TestContext.Current.CancellationToken);
    await model.LoadActiveBansAsync("user-1", TestContext.Current.CancellationToken);
    await model.LoadMoreCommunityListAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, model.BanRows.Count);
    Assert.Contains("after=opaque%2F%2B%3F%3D", handler.Paths[^1], StringComparison.Ordinal);
    await model.LoadBanHistoryAsync(TestContext.Current.CancellationToken);
    await model.LoadMoreCommunityListAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, model.BanRows.Count);
    Assert.Contains("/bans?after=opaque%2F%2B%3F%3D", handler.Paths[^1], StringComparison.Ordinal);
  }

  [Fact]
  public async Task SwitchingScopeRejectsAnOlderInitialActivePage()
  {
    var handler = new BanHandler { PausedActive = new(TaskCreationOptions.RunContinuationsAsynchronously) };
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var model = new CommunityDetailViewModel(new ApiCommunitiesService(new VouchaApiClient(http)));
    var oldLoad = model.LoadActiveBansAsync("old-user", TestContext.Current.CancellationToken);
    await model.LoadBanHistoryAsync(TestContext.Current.CancellationToken);
    handler.PausedActive.SetResult();
    await oldLoad;
    Assert.Null(model.ActiveBansUserId);
    Assert.DoesNotContain(model.BanRows, row => row.Id == "stale-ban");
  }

  private sealed class BanHandler : HttpMessageHandler
  {
    public List<string> Paths { get; } = [];
    public string? Failure { get; init; }
    public bool Paged { get; init; }
    public TaskCompletionSource? PausedActive { get; init; }
    public HttpStatusCode Status { get; init; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Paths.Add(request.RequestUri!.PathAndQuery);
      var stale = PausedActive is not null && request.RequestUri.AbsolutePath.Contains("/users/", StringComparison.Ordinal);
      if (stale) await PausedActive!.Task;
      var id = request.Method == HttpMethod.Delete
          ? Failure is not null ? $"lift-all.{Failure}" : request.RequestUri.AbsolutePath.Contains("/users/", StringComparison.Ordinal) ? "lift-all.default" : "lift-one.default"
          : request.RequestUri.AbsolutePath.Contains("/users/", StringComparison.Ordinal) || request.RequestUri.AbsolutePath.EndsWith("/bans", StringComparison.Ordinal) ? "active.default" : "detail.default";
      var body = ApiFixtureLoader.LoadResponse($"native.communities.bans.{id}");
      if (id == "active.default")
      {
        var node = JsonNode.Parse(body)!;
        if (Paged) node["page_info"]!["end_cursor"] = "opaque/+?=";
        var original = node["community_bans"]!.AsObject().First().Value!.DeepClone();
        var sibling = stale ? "stale-ban" : "sibling-ban";
        original["id"] = sibling;
        node["community_bans"]![sibling] = original;
        node["results"]!.AsArray().Add(new JsonObject { ["id"] = sibling });
        body = node.ToJsonString();
      }
      return new HttpResponseMessage(request.Method == HttpMethod.Delete && Failure is not null ? Status : HttpStatusCode.OK)
      { Content = new StringContent(body) };
    }
  }
}

using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.ReferralLinks;
using Xunit;

namespace Voucha.Client.Core.Tests.ReferralLinks;

public sealed class ReferralPriorityPresentationTests
{
  [Theory]
  [InlineData(1, true, "Priority group 1")]
  [InlineData(2, false, "Priority group 2")]
  [InlineData(3, false, "Priority group 3")]
  [InlineData(4, false, "Priority group 4")]
  [InlineData(5, true, "Use our official links")]
  [InlineData(5, false, "Use our official links")]
  [InlineData(6, false, "Everyone Else")]
  [InlineData(6, true, "Everyone Else")]
  public async Task GroupCopyAndOfficialBadgeFollowIndependentServerFields(int group, bool official, string detail)
  {
    using var handler = new Handler([Link("link", group, official)]);
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    using var model = new ReferralLinksViewModel(new ApiReferralLinksService(new(http)));

    await model.LoadPrioritizedAsync("program", cancellationToken: TestContext.Current.CancellationToken);

    var row = Assert.Single(model.Items);
    Assert.Equal(detail, row.Detail);
    Assert.Equal(official, row.HasOfficialBadge);
    Assert.Equal(official ? "Official Voucha" : null, row.LocalizedOfficialBadge);
    UiMessageKey? expectedBadgeKey = official
        ? UiMessageKey.ExtractedReferralLinksReferralLinkCardOfficialVouchaA2d311b4 : null;
    Assert.Equal(expectedBadgeKey, row.OfficialBadgeText?.Key);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task PreserveServerOrderIncludingNeutralAllMode(bool all)
  {
    var links = all
        ? new[] { Link("z", 6, false), Link("a", 6, true), Link("b", 6, false) }
        : new[] { Link("z", 6, false), Link("a", 5, true), Link("b", 1, false), Link("c", 6, true) };
    using var handler = new Handler(links);
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    using var model = new ReferralLinksViewModel(new ApiReferralLinksService(new(http)));

    await model.LoadPrioritizedAsync("program", all, TestContext.Current.CancellationToken);

    Assert.Equal(links.Select(link => link.Id), model.Items.Select(row => row.Id));
    Assert.Equal(all ? "?all=true" : string.Empty, Assert.Single(handler.Requests).Query);
    Assert.Equal([false, true, false], model.Items.Take(3).Select(row => row.HasOfficialBadge));
    if (all) Assert.All(model.Items, row => Assert.Equal("Everyone Else", row.Detail));
  }

  [Fact]
  public async Task LocaleRefreshReprojectsCopyWithoutChangingOrderOrReloading()
  {
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    using var handler = new Handler([Link("z", 6, true), Link("a", 5, false)]);
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    using var model = new ReferralLinksViewModel(new ApiReferralLinksService(new(http)), localization, controller);
    await model.LoadPrioritizedAsync("program", all: false, TestContext.Current.CancellationToken);
    var englishRows = model.Items;
    Assert.Equal("Everyone Else", englishRows[0].Detail);
    Assert.Equal("Official Voucha", englishRows[0].LocalizedOfficialBadge);
    Assert.Equal("Use our official links", englishRows[1].Detail);

    controller.ApplySavedLocale("fr");

    Assert.NotSame(englishRows, model.Items);
    Assert.Equal(["z", "a"], model.Items.Select(row => row.Id));
    Assert.Equal("Tous les Autres", model.Items[0].Detail);
    Assert.Equal("Officiel Voucha", model.Items[0].LocalizedOfficialBadge);
    Assert.Equal("Utilisez nos liens officiels", model.Items[1].Detail);
    Assert.Single(handler.Requests);
  }

  private static PrioritizedReferralLink Link(string id, int group, bool official) =>
      new(id, "member", official, "program", "https://example.test/" + id, id,
          group, id[0], id[0], id[0], null, null, null);

  private sealed class Handler(IReadOnlyList<PrioritizedReferralLink> links) : HttpMessageHandler
  {
    public List<Uri> Requests { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Requests.Add(request.RequestUri!);
      var response = new PrioritizedReferralLinksResponse(links,
          new Dictionary<string, ReferralLinkFeedUser>(StringComparer.Ordinal));
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(JsonSerializer.Serialize(response, VouchaApiJson.Options)),
      });
    }
  }
}

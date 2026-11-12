using System.Net;
using System.Text.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.ReferralLinks;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ReferralPriorityRenderingTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task RealReferralCardsRenderIndependentBadgeAndGroupCopyAcrossLocales(bool all)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    using var localeVersion = new UiLocaleVersion(controller);
    var app = new Application();
    foreach (var key in new[] { "Body", "Headline", "Metadata" }) app.Resources[key] = new Style(typeof(Label));
    app.Resources["UiLocaleVersion"] = localeVersion;
    app.Resources["UiLocalizedValue"] = new UiLocalizedValueConverter(localization);
    using var handler = new Handler(all);
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    using var model = new ReferralLinksViewModel(new ApiReferralLinksService(new(http)), localization, controller);
    var page = new ReferralLinksPage(model, new Session(), new ReferralLinksRouteContextStore());

    await model.LoadPrioritizedAsync("program", all, TestContext.Current.CancellationToken);

    var collection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(page));
    var englishSource = collection.ItemsSource;
    Assert.Equal(["z", "a", "b", "c"], model.Items.Select(row => row.Id));
    var englishCards = Render(collection);
    Assert.Equal(all ? ["Everyone Else", "Everyone Else", "Everyone Else", "Everyone Else"]
        : new[] { "Everyone Else", "Use our official links", "Use our official links", "Priority group 1" },
        englishCards.Select(card => card.Detail));
    Assert.Equal([false, true, false, true], englishCards.Select(card => card.HasBadge));
    Assert.Equal([null, "Official Voucha", null, "Official Voucha"], englishCards.Select(card => card.Badge));

    controller.ApplySavedLocale("fr");

    Assert.NotSame(englishSource, collection.ItemsSource);
    Assert.Equal(["z", "a", "b", "c"], model.Items.Select(row => row.Id));
    var frenchCards = Render(collection);
    Assert.Equal("Tous les Autres", frenchCards[0].Detail);
    Assert.Equal(all ? "Tous les Autres" : "Utilisez nos liens officiels", frenchCards[1].Detail);
    Assert.Equal(all ? "Tous les Autres" : "Utilisez nos liens officiels", frenchCards[2].Detail);
    Assert.Equal([null, "Officiel Voucha", null, "Officiel Voucha"], frenchCards.Select(card => card.Badge));
    Assert.Equal([false, true, false, true], frenchCards.Select(card => card.HasBadge));
    Assert.Equal(1, handler.Requests);
    Assert.Equal(all, handler.RequestedAll);
  }

  private static Card[] Render(CollectionView collection) =>
      Assert.IsAssignableFrom<IEnumerable<ReferralLinkRow>>(collection.ItemsSource).Select(row =>
      {
        var content = Assert.IsType<Border>(collection.ItemTemplate.CreateContent());
        content.BindingContext = row;
        var badge = Find<Border>(content, "referral-official-badge-container");
        return new Card(Find<Label>(content, "referral-link-detail").Text,
            Find<Label>(content, "referral-official-badge").Text, badge.IsVisible);
      }).ToArray();

  private static T Find<T>(Element root, string id) where T : Element =>
      Assert.Single(VisualTreeElements.Descendants<T>(root), element => element.AutomationId == id);

  private sealed record Card(string Detail, string? Badge, bool HasBadge);

  private sealed class Handler(bool all) : HttpMessageHandler
  {
    public int Requests { get; private set; }
    public bool RequestedAll { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Requests++;
      RequestedAll = request.RequestUri!.Query == "?all=true";
      var links = new[] { Link("z", 6, false), Link("a", 5, true), Link("b", 5, false), Link("c", 1, true) };
      var response = new PrioritizedReferralLinksResponse(all
          ? links.Select(link => link with { PriorityGroup = 6 }).ToArray() : links,
          new Dictionary<string, ReferralLinkFeedUser>(StringComparer.Ordinal));
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(JsonSerializer.Serialize(response, VouchaApiJson.Options)),
      });
    }

    private static PrioritizedReferralLink Link(string id, int group, bool official) =>
        new(id, "member", official, "program", "https://example.test/" + id, id,
            group, id[0], id[0], id[0], null, null, null);
  }

  private sealed class Session : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User("member", "member"));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

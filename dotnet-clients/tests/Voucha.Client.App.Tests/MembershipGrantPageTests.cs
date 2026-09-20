using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.MembershipAdministration;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class MembershipGrantPageTests
{
  [Fact]
  public async Task RetryRemainsVisibleForUserContentPlanError()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider()); _ = new Application();
    var controller = new UiLocaleController(new Languages()); var localization = new UiLocalization(controller);
    var model = new MembershipGrantViewModel(new ErrorService(), localization);
    await model.LoadPlansAsync(TestContext.Current.CancellationToken);
    var page = new MembershipGrantPage(model, localization, controller);
    Assert.True(Assert.Single(Descendants<Button>(page), value => value.AutomationId == "membership-grant-retry").IsVisible);
  }
  [Fact]
  public async Task RendersDedicatedLocalizedGrantControls()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var controller = new UiLocaleController(new Languages());
    var localization = new UiLocalization(controller);
    var model = new MembershipGrantViewModel(new Service(), localization);
    await model.LoadPlansAsync(TestContext.Current.CancellationToken);
    var page = new MembershipGrantPage(model, localization, controller);

    Assert.Equal("Membership grants", page.Title);
    Assert.Single(Descendants<Entry>(page), value => value.AutomationId == "membership-grant-query");
    Assert.Single(Descendants<Entry>(page), value => value.AutomationId == "membership-grant-duration-days");
    Assert.Single(Descendants<Button>(page), value => value.AutomationId == "membership-grant-search");
    Assert.Single(Descendants<Button>(page), value => value.AutomationId == "membership-grant-submit");
    Assert.Single(Descendants<Button>(page), value => value.AutomationId == "membership-grant-retry");
    Assert.Contains(Descendants<Label>(page), value => value.TextColor == Colors.IndianRed);
    Assert.Contains(Descendants<Label>(page), value => value.TextColor == Colors.ForestGreen);
    Assert.Empty(Descendants<WebView>(page));
    var candidateButton = Assert.IsType<Button>(Assert.Single(Descendants<CollectionView>(page)).ItemTemplate.CreateContent());
    candidateButton.BindingContext = new UserSearchResult("user-without-username", null);
    Assert.Equal("user-without-username", candidateButton.Text);
    candidateButton.BindingContext = new UserSearchResult("user-with-username", "alice");
    Assert.Equal("alice", candidateButton.Text);

    controller.ApplySavedLocale("fr");
    Assert.Equal("Attributions d’abonnement", page.Title);

    var plan = Assert.Single(Descendants<Picker>(page), value => value.AutomationId == "membership-grant-plan");
    var sku = Assert.Single(Descendants<Picker>(page), value => value.AutomationId == "membership-grant-sku");
    plan.SelectedIndex = 0;
    Assert.Equal(MembershipGrantPlanSlug.Plus, model.SelectedPlan);
    Assert.Equal(MembershipGrantPlanSlug.Plus, ((MembershipGrantPlanOption)plan.SelectedItem).Value);
    sku.SelectedIndex = 0;
    Assert.NotNull(model.SelectedSku);
    Assert.Equal(model.SelectedSku, ((MembershipGrantSkuOption)sku.SelectedItem).Value);
    Assert.Contains("Annuel", ((IEnumerable<MembershipGrantSkuOption>)sku.ItemsSource).Last().Label, StringComparison.Ordinal);
    var skuOptions = ((IEnumerable<MembershipGrantSkuOption>)sku.ItemsSource).ToArray();
    Assert.Equal(3, skuOptions.Select(option => option.Label).Distinct().Count());
    Assert.Contains(skuOptions, option => option.Label.Contains("sku-monthly-a", StringComparison.Ordinal));
    Assert.Contains(skuOptions, option => option.Label.Contains("sku-monthly-b", StringComparison.Ordinal));

    model.SelectUser(new UserSearchResult("user-1", "alice"));
    model.DurationDays = "30";
    Assert.True(await model.GrantAsync(TestContext.Current.CancellationToken));
    Assert.Equal(
        localization.Localize(UiMessageKey.NativeSwiftMembershipMembershipGrantSuccess),
        Assert.Single(Descendants<Label>(page), value => value.TextColor == Colors.ForestGreen).Text);
    Assert.Empty(Assert.Single(Descendants<Label>(page), value => value.TextColor == Colors.IndianRed).Text);
    Assert.False(await model.GrantAsync(TestContext.Current.CancellationToken));
    Assert.Empty(Assert.Single(Descendants<Label>(page), value => value.TextColor == Colors.ForestGreen).Text);
    Assert.NotEmpty(Assert.Single(Descendants<Label>(page), value => value.TextColor == Colors.IndianRed).Text);

    page.Dispose();
    controller.ApplySavedLocale("es");
    Assert.Equal("Attributions d’abonnement", page.Title);
  }

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>();

  private sealed class Languages : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages => ["en"];
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => new ImmediateDispatcher();
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }

  private sealed class Service : IMembershipAdministrationService
  {
    public Task<MembershipPlansResponse> FetchPlansAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new MembershipPlansResponse(
        [
          Product("sku-monthly-a", "monthly", "price-monthly-a"),
          Product("sku-monthly-b", "monthly", "price-monthly-b"),
          Product("sku-yearly", "yearly", "price-yearly"),
        ]));
    public Task<UsersSearchResponse> SearchUsersAsync(SearchUsersRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new UsersSearchResponse([], new PageInfo(null, false, null)));
    public Task<GrantMembershipResponse> GrantAsync(GrantMembershipBody body, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GrantMembershipResponse(
            new MembershipGrantResult("grant"),
            new MembershipGrantResult("membership"),
            false));

    private static MembershipCatalogProduct Product(string id, string interval, string priceId) =>
        new(id, "plus", interval, [new MembershipCatalogProvider("stripe", "test", "voucha-web", priceId, null, null, null, new Money(500, "usd"))]);
  }

  private sealed class ErrorService : IMembershipAdministrationService
  {
    public Task<MembershipPlansResponse> FetchPlansAsync(CancellationToken cancellationToken = default) =>
        Task.FromException<MembershipPlansResponse>(new VouchaApiException(System.Net.HttpStatusCode.TooManyRequests, "{\"message\":\"Try later\"}"));
    public Task<UsersSearchResponse> SearchUsersAsync(SearchUsersRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<GrantMembershipResponse> GrantAsync(GrantMembershipBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }
}

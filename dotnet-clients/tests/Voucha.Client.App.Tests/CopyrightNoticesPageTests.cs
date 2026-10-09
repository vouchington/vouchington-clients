using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Copyright;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CopyrightNoticesPageTests
{
  [Fact]
  public async Task ListLinksOnlyTheCurrentPublicClaimantAndAppendsTheNextPage()
  {
    PrepareMaui();
    var service = new CaseService();
    var model = new CopyrightNoticesViewModel(service, new NavigationViewer(true, []));
    using var locales = new UiLocaleController(new EnglishLanguages());
    NativeRoutePath? navigated = null;
    using var page = new CopyrightNoticesPage(model, locales, path => { navigated = path; return Task.CompletedTask; });
    await page.LoadAsync(TestContext.Current.CancellationToken);
    page.OnUiLocaleChanged();
    var claimant = Find<Button>(page, "copyright-claimant-case-1");
    Assert.Equal("Current public name", claimant.Text);
    claimant.SendClicked();
    Assert.Equal(NativeRoutePath.Entity("user", "user-1"), navigated);
    Assert.DoesNotContain(Descendants<Button>(page), button => button.AutomationId == "copyright-claimant-case-2");
    Find<Button>(page, "copyright-load-more").SendClicked();
    page.OnUiLocaleChanged();
    Assert.Equal([null, "opaque/+?="], service.Cursors);
    Assert.NotNull(Find<Button>(page, "copyright-case-case-3"));
    Assert.DoesNotContain(Descendants<Button>(page), button => button.AutomationId == "copyright-load-more");
  }

  [Fact]
  public async Task SignedOutPageOffersNativeSignInWithoutFetchingPrivateCases()
  {
    PrepareMaui();
    var service = new CaseService();
    var model = new CopyrightNoticesViewModel(service, NavigationViewer.Anonymous);
    using var locales = new UiLocaleController(new EnglishLanguages());
    NativeRoutePath? navigated = null;
    using var page = new CopyrightNoticesPage(model, locales, path => { navigated = path; return Task.CompletedTask; });
    await page.LoadAsync(TestContext.Current.CancellationToken);
    Find<Button>(page, "copyright-sign-in").SendClicked();
    Assert.Equal(NativeRoutePath.Segments("login"), navigated);
    Assert.Empty(service.Cursors);
  }

  [Fact]
  public async Task VisibleDatesRefreshWhenTheLocaleChanges()
  {
    PrepareMaui();
    using var locales = new UiLocaleController(new EnglishLanguages());
    var localization = new UiLocalization(locales);
    UiCopy.UseLocalization(localization);
    var model = new CopyrightNoticesViewModel(new CaseService(), new NavigationViewer(true, []));
    using var page = new CopyrightNoticesPage(model, locales, _ => Task.CompletedTask);
    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
    var appear = typeof(CopyrightNoticesPage).GetMethod("OnAppearing", flags, Type.EmptyTypes)!;
    var disappear = typeof(CopyrightNoticesPage).GetMethod("OnDisappearing", flags, Type.EmptyTypes)!;
    try
    {
      appear.Invoke(page, null);
      await page.LoadAsync(TestContext.Current.CancellationToken);
      var instant = model.Notices[0].AcceptedAt;
      var before = localization.Format(UiMessageKey.NativeCopyrightNoticesAcceptedDate,
          ("date", localization.FormatDateTime(instant, TimeZoneInfo.Local)));
      Assert.Contains(Descendants<Label>(page), label => label.Text == before);
      locales.ApplySavedLocale("fr");
      var after = localization.Format(UiMessageKey.NativeCopyrightNoticesAcceptedDate,
          ("date", localization.FormatDateTime(instant, TimeZoneInfo.Local)));
      Assert.NotEqual(before, after);
      Assert.Contains(Descendants<Label>(page), label => label.Text == after);
      Assert.DoesNotContain(Descendants<Label>(page), label => label.Text == before);
    }
    finally
    {
      disappear.Invoke(page, null);
      UiCopy.UseLocalization(UiLocalization.English);
    }
  }

  private static CopyrightNoticeSummary Notice(string id, CopyrightPublicClaimant? claimant) => new()
  {
    Id = id, Jurisdiction = "us", ReceivedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
    AcceptedAt = DateTimeOffset.Parse("2026-01-02T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
    ProvisionalWithholdingAt = null, TargetCount = 1, Claimant = claimant,
  };

  private sealed class CaseService : ICopyrightNoticesService
  {
    public List<string?> Cursors { get; } = [];
    public Task<CopyrightNoticesResponse> FetchPageAsync(string? after, int limit, CancellationToken cancellationToken)
    {
      Cursors.Add(after);
      return Task.FromResult(after is null
          ? new CopyrightNoticesResponse([Notice("case-1", new("user-1", "Current public name")), Notice("case-2", null)], new("opaque/+?=", true, null))
          : new CopyrightNoticesResponse([Notice("case-3", null)], new(null, false, null)));
    }
    public Task<CopyrightNoticeResponse> FetchDetailAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<CopyrightParticipantNoticeResponse> FetchParticipantAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<CopyrightEuDisputeSettlementsResponse> FetchSettlementsAsync(string id, string after, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();
  }

  private static void PrepareMaui()
  {
    UiCopy.UseLocalization(UiLocalization.English);
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in UiMessageKey.All) app.Resources[key.Value] = UiLocalization.English.Localize(key);
  }
  private static T Find<T>(Element root, string id) where T : Element =>
      Assert.Single(Descendants<T>(root), element => element.AutomationId == id);
  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }
  private sealed class EnglishLanguages : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages => ["en"];
  }
  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }
  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}

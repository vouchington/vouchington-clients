using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.IdentityVerificationAdministration;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class IdentityVerificationAttemptGrantPageTests
{
  [Fact]
  public async Task RendersLocalizedGrantControlsWithoutWebView()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var controller = new UiLocaleController(new Languages());
    var localization = new UiLocalization(controller);
    var model = new IdentityVerificationAttemptGrantViewModel(new Service(), localization);
    model.Configure("alice", true);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = new IdentityVerificationAttemptGrantPage(model, localization, controller);

    Assert.Equal("Identity verification", page.Title);
    Assert.Single(Descendants<Editor>(page), value => value.AutomationId == "identity-verification-grant-note");
    Assert.Single(Descendants<Button>(page), value => value.AutomationId == "identity-verification-grant-submit");
    Assert.Empty(Descendants<WebView>(page));

    controller.ApplySavedLocale("fr");
    Assert.Equal("Verification d'identite", page.Title);
    page.Dispose();
  }

  [Fact]
  public async Task HidesGrantControlsForCustomerSupport()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var controller = new UiLocaleController(new Languages());
    var localization = new UiLocalization(controller);
    var model = new IdentityVerificationAttemptGrantViewModel(new Service(), localization);
    var page = new IdentityVerificationAttemptGrantPage(model, localization, controller);
    await page.ApplyRouteAsync("alice", false);

    Assert.False(Assert.Single(Descendants<Editor>(page)).IsVisible);
    Assert.False(Assert.Single(Descendants<Button>(page), value => value.AutomationId == "identity-verification-grant-submit").IsVisible);
    page.Dispose();
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

  private sealed class Service : IIdentityVerificationAdministrationService
  {
    public Task<UserResponse> FetchUserAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserResponse(new User("user-1", "alice")));

    public Task<GrantIdentityVerificationAttemptResponse> GrantAsync(
        string userId,
        string note,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GrantIdentityVerificationAttemptResponse(true));
  }
}

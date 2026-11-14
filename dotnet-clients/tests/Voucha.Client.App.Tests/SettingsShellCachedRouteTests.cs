using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class SettingsShellCachedRouteTests
{
  [Fact]
  public async Task PreferencesRouteUpdatesTheExistingTemplatePageAndEndsNotificationActivation()
  {
    PrepareMaui();
    var service = DispatchProxy.Create<ISettingsService,
        SettingsCredentialsRenderedControlsTests.CredentialSettingsService>();
    using var model = new SettingsViewModel(service);
    var page = new SettingsPage(model);
    var viewer = new NavigationViewer(true, []);
    var notification = NativeDeepLinkResolver.Resolve("voucha://my/notification-settings", viewer).Match!;
    var preferences = NativeDeepLinkResolver.Resolve("voucha://my/preferences", viewer).Match!;
    page.ApplyInitialRouteMatch(notification);
    Assert.True(page.NotificationSectionVisibleForTest);

    var creations = 0;
    var content = new ShellContent
    {
      Route = NavigationCatalog.SettingsIntentId,
      ContentTemplate = new DataTemplate(() => { creations++; return page; }),
    };
    var shell = new AppShell();
    shell.Items.Add(new FlyoutItem { Items = { new Tab { Items = { content } } } });
    ((IShellContentController)content).GetOrCreateContent();
    // This is the real MAUI template cache shape that the old Content-only lookup missed.
    Assert.Null(content.Content);

    var directory = Path.Combine(Path.GetTempPath(), $"voucha-settings-route-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var result = Path.Combine(directory, "audit.json");
    var priorResult = Environment.GetEnvironmentVariable("CATALYST_GUI_RESULT");
    try
    {
      Environment.SetEnvironmentVariable("CATALYST_GUI_RESULT", result);
#if DEBUG
      var audit = CatalystGuiAudit.Start(new JsonObject())!;
      // Seed an already-active notification route; departure uses the real page lifecycle hook.
      audit.Activate("/my/notification-settings");
#endif

      var shouldStoreForFutureCreation = await shell.ApplySettingsRouteForTestAsync(preferences);

      Assert.False(shouldStoreForFutureCreation);
      Assert.False(page.NotificationSectionVisibleForTest);
#if DEBUG
      Assert.False(JsonNode.Parse(File.ReadAllText(result + ".app.json"))!["notificationRouteActive"]!.GetValue<bool>());
#endif
      Assert.Equal(1, creations);
      Assert.Same(page, ((IShellContentController)content).Page);
    }
    finally
    {
#if DEBUG
      Environment.SetEnvironmentVariable("CATALYST_GUI_RESULT", null);
      CatalystGuiAudit.Start(new JsonObject());
#endif
      Environment.SetEnvironmentVariable("CATALYST_GUI_RESULT", priorResult);
      Directory.Delete(directory, true);
    }
  }

  [Fact]
  public async Task UncreatedTemplateRemainsDeferredWhenStoringSettingsRoute()
  {
    PrepareMaui();
    var content = new ShellContent
    {
      Route = NavigationCatalog.SettingsIntentId,
      ContentTemplate = new DataTemplate(() => throw new InvalidOperationException("Must remain lazy")),
    };
    var shell = new AppShell();
    shell.Items.Add(new FlyoutItem { Items = { new Tab { Items = { content } } } });
    var match = NativeDeepLinkResolver.Resolve("/my/preferences", new NavigationViewer(true, [])).Match!;

    Assert.True(await shell.ApplySettingsRouteForTestAsync(match));
    Assert.Null(((IShellContentController)content).Page);
  }

  private static void PrepareMaui()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var application = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
      },
    };
    foreach (var key in UiMessageKey.All)
      application.Resources[key.Value] = UiLocalization.English.Localize(key);
  }
}

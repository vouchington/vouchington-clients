using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.Localization;

public sealed class UiLocalizationTests
{
  [Fact]
  public void SavedSupportedLocaleTakesPrecedenceOverDeviceLocale()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("fr-FR"));

    controller.ApplySavedLocale("es");

    Assert.Equal("es", controller.EffectiveLocale);
  }

  [Theory]
  [InlineData("de", "fr")]
  [InlineData(null, "fr")]
  [InlineData("", "fr")]
  public void UnsupportedOrClearedSavedLocaleFallsBackToNormalizedDeviceLocale(
      string? savedLocale,
      string expected)
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("fr-CA"));

    controller.ApplySavedLocale(savedLocale);

    Assert.Equal(expected, controller.EffectiveLocale);
  }

  [Fact]
  public void UnsupportedDeviceLocaleFallsBackToEnglish()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("de-DE"));

    Assert.Equal("en", controller.EffectiveLocale);
  }

  [Fact]
  public void LocaleChangesNotifyVisibleConsumersWithoutRestart()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en-US"));
    var changes = new List<string>();
    controller.LocaleChanged += (_, _) => changes.Add(controller.EffectiveLocale);

    controller.ApplySavedLocale("pt-BR");
    Assert.Equal("pt", controller.Culture.TwoLetterISOLanguageName);
    controller.ApplySavedLocale(null);

    Assert.Equal(["pt", "en"], changes);
  }

  [Fact]
  public void AlreadyVisibleLocalizedTextResolvesCopyAgainAfterLocaleChanges()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en-US"));
    var localization = new UiLocalization(controller);
    var visibleCopy = UiText.Localized(UiMessageKey.CommonCancel);

    Assert.Equal("Cancel", localization.Resolve(visibleCopy));

    controller.ApplySavedLocale("fr");

    Assert.Equal("Annuler", localization.Resolve(visibleCopy));
  }

  [Fact]
  public void OverlayValuesWinOverBundledResourcesUntilReset()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en-US"));
    var overlay = new LocalizationValueCache();
    overlay.Apply(
        "en",
        "rev-1",
        60,
        new Dictionary<string, string> { ["common.cancel"] = "Abort" },
        DateTimeOffset.UnixEpoch);
    var localization = new UiLocalization(controller, overlay);

    Assert.Equal("Abort", localization.Localize(UiMessageKey.CommonCancel));
    overlay.Reset();
    Assert.Equal("Cancel", localization.Localize(UiMessageKey.CommonCancel));
  }

  [Fact]
  public void SessionLocaleChangesAndSignOutReapplyLocalePrecedence()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("fr-CA"));
    var sessionStore = new StubSessionStore();
    controller.AttachSession(sessionStore);

    sessionStore.SetIdentity(new User("user-1", "alice", UiLocale: "pt-BR"));
    Assert.Equal("pt", controller.EffectiveLocale);

    sessionStore.SetIdentity(new User("user-1", "alice", UiLocale: "de-DE"));
    Assert.Equal("fr", controller.EffectiveLocale);

    sessionStore.SetIdentity(null);
    Assert.Equal("fr", controller.EffectiveLocale);
  }

  [Fact]
  public void AttachingAnotherSessionStopsObservingThePreviousStore()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en-US"));
    var oldStore = new StubSessionStore();
    var currentStore = new StubSessionStore();
    controller.AttachSession(oldStore);
    controller.AttachSession(currentStore);

    oldStore.SetIdentity(new User("old", "old", UiLocale: "es"));
    currentStore.SetIdentity(new User("current", "current", UiLocale: "fr"));

    Assert.Equal("fr", controller.EffectiveLocale);
  }

  [Fact]
  public void LocalizerReadsGeneratedResourcesAndInterpolates()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("es"));
    var localizer = new UiLocalization(controller);

    Assert.Equal("Cancelar", localizer.Localize(UiMessageKey.CommonCancel));
    Assert.Contains("3", localizer.Format(UiMessageKey.SettingsLanguageSupportedCount, ("count", 3)));
  }

  [Fact]
  public void LocalizerSelectsPluralAndSelectPluralResources()
  {
    var localizer = new UiLocalization(
        new UiLocaleController(new StubDeviceLanguageProvider("es-MX")));

    Assert.Equal(
        "1 idioma",
        localizer.Format(UiMessageKey.SettingsLanguageSupportedCount, ("count", 1)));
    Assert.Equal(
        "2 idiomas",
        localizer.Format(UiMessageKey.SettingsLanguageSupportedCount, ("count", 2)));
    Assert.Equal(
        "2 publicaciones",
        localizer.Format(
            UiMessageKey.SharedCountLabelFormat,
            ("unit", "post"),
            ("count", 2)));
  }

  [Fact]
  public void LocalizerRejectsMissingOrUnknownDescriptorArguments()
  {
    var localizer = new UiLocalization(
        new UiLocaleController(new StubDeviceLanguageProvider("en-US")));

    Assert.Throws<ArgumentException>(() =>
        localizer.Format(UiMessageKey.SettingsLanguageSupportedCount));
    Assert.Throws<ArgumentOutOfRangeException>(() =>
        localizer.Format(
            UiMessageKey.SharedCountLabelFormat,
            ("unit", "unknown"),
            ("count", 2)));
  }

  [Fact]
  public void FormattingUsesPresentationLocaleWithoutChangingInstantOrCurrencyCode()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("fr"));
    var localizer = new UiLocalization(controller);
    var instant = new DateTimeOffset(2026, 7, 14, 18, 30, 0, TimeSpan.Zero);

    var offsetTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "test-zone",
        TimeSpan.FromHours(2),
        "Test zone",
        "Test zone");
    var date = localizer.FormatDateTime(instant, offsetTimeZone);
    var number = localizer.FormatNumber(1234.5m);
    var percent = localizer.FormatPercent(0.125m);
    var currency = localizer.FormatCurrency(12.5m, "USD");

    Assert.Contains("2026", date);
    Assert.Contains("20:30", date);
    Assert.Contains(',', number);
    Assert.Contains('%', percent);
    Assert.Contains("USD", currency);
    Assert.Equal(instant, instant.ToUniversalTime());
  }

  [Fact]
  public void VerbatimTextHasAnExplicitBoundary()
  {
    var localizer = new UiLocalization(
        new UiLocaleController(new StubDeviceLanguageProvider("pt")));

    Assert.Equal("User supplied", localizer.Resolve(UiText.Verbatim("User supplied")));
    Assert.Equal("Cancelar", new UiLocalization(
        new UiLocaleController(new StubDeviceLanguageProvider("es")))
        .Resolve(UiText.Localized(UiMessageKey.CommonCancel)));
    Assert.Equal(
        "2 idiomas",
        new UiLocalization(new UiLocaleController(new StubDeviceLanguageProvider("es")))
            .Resolve(UiText.Localized(
                UiMessageKey.SettingsLanguageSupportedCount,
                ("count", 2))));
  }

  [Fact]
  public void NestedUiTextArgumentsResolveWithTheActiveLocale()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localizer = new UiLocalization(controller);
    var message = UiText.Localized(
        UiMessageKey.NativeDotnetMediaPlaybackJumpTo,
        ("target", UiText.Localized(UiMessageKey.CommonCancel)));

    Assert.Contains("Cancel", localizer.Resolve(message));

    controller.ApplySavedLocale("fr");

    Assert.Contains("Annuler", localizer.Resolve(message));
    Assert.DoesNotContain("Cancel", localizer.Resolve(message));
  }

  [Fact]
  public void LocalizedValueFormatterFormatsBindingsWithThePresentationLocale()
  {
    var localizer = new UiLocalization(
        new UiLocaleController(new StubDeviceLanguageProvider("es")));
    var formatter = new UiLocalizedValueFormatter(localizer);
    var instant = new DateTimeOffset(2026, 7, 14, 18, 30, 0, TimeSpan.Zero);

    Assert.Equal(
        "Clics: 1.234,5",
        formatter.Format(1234.5m, "message:native.dotnet.landingPages.clickCount|number"));
    Assert.Equal("12,5%", formatter.Format(0.125m, "percent"));
    Assert.Equal("+12,5", formatter.Format(12.5m, "signedNumber"));
    Assert.Contains("2026", formatter.Format(instant, "dateTime"));
  }

  [Fact]
  public void LocalizedValueFormatterRefreshesDateTimeAfterLocaleChange()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var formatter = new UiLocalizedValueFormatter(new UiLocalization(controller));
    var instant = new DateTimeOffset(2026, 7, 14, 18, 30, 0, TimeSpan.Zero);

    var english = formatter.Format(instant, "dateTime");
    controller.ApplySavedLocale("fr");

    var french = formatter.Format(instant, "dateTime");
    Assert.NotEqual(english, french);
    Assert.Contains("2026", french);
  }

  [Fact]
  public void LocalizedValueFormatterRejectsUnknownModes()
  {
    var formatter = new UiLocalizedValueFormatter(
        new UiLocalization(new UiLocaleController(new StubDeviceLanguageProvider("en"))));

    Assert.Throws<ArgumentException>(() => formatter.Format(1, "unknown"));
    Assert.Throws<ArgumentNullException>(() => formatter.Format(1, null));
  }


  private sealed class StubDeviceLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }

  private sealed class StubSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public SessionSnapshot Current { get; private set; } = SessionSnapshot.Anonymous;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
      SetIdentity(null);
      return Task.CompletedTask;
    }

    public void SetIdentity(User? identity)
    {
      Current = new SessionSnapshot(identity);
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
    }
  }
}

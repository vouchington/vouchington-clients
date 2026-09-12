using Xunit;

namespace Voucha.Client.Core.Tests.Localization;

public sealed class MauiLocalizationWiringTests
{
  [Fact]
  public void TransientLocalizedViewModelsUseWeakLocaleSubscriptions()
  {
    var sources = new[]
    {
      "Settings/SettingsViewModel.cs",
      "NewsFeeds/NewsFeedsViewModel.cs",
      "Growth/GrowthDashboardViewModel.cs",
      "Moderation/ModerationViewModel.cs",
      "Search/OmnisearchViewModel.cs",
      "Topics/TopicDetailViewModel.cs",
      "Friends/FriendsViewModel.cs",
      "Notifications/NotificationsViewModel.cs",
    };

    foreach (var relativePath in sources)
    {
      var source = ReadCoreTypeSources(relativePath);
      Assert.Contains("IUiLocaleChangeListener", source, StringComparison.Ordinal);
      Assert.Contains("SubscribeLocaleChanges(this)", source, StringComparison.Ordinal);
      Assert.DoesNotContain("LocaleChanged += OnLocaleChanged", source, StringComparison.Ordinal);
    }
  }

  [Fact]
  public void MauiFactoriesInjectLiveLocalizationIntoSearchAndDirectMessages()
  {
    var source = ReadAppSource("MauiProgram.cs");

    Assert.Contains(
        "sp.GetRequiredService<IUiLocalization>()",
        source,
        StringComparison.Ordinal);
    Assert.Contains(
        "sp.GetRequiredService<IUiLocaleController>()",
        source,
        StringComparison.Ordinal);
    Assert.Contains("AddSingleton<LocalizationValueCache>()", source, StringComparison.Ordinal);
    Assert.Contains("AddSingleton<LocalizationRefreshService>()", source, StringComparison.Ordinal);
    var navigation = ReadAppSource("MauiProgram.NavigationFeatureFlags.cs");
    Assert.Contains("RefreshChromeAsync()", navigation, StringComparison.Ordinal);
    Assert.Contains(
        "new DirectMessagesViewModel(",
        source,
        StringComparison.Ordinal);
  }

  [Fact]
  public void LocaleRefreshUpdatesShellTitlesWithoutRebuildingNavigation()
  {
    var source = ReadAppSource("AppShell.Localization.cs");

    Assert.Contains("RefreshNavigationLocalization", source, StringComparison.Ordinal);
    Assert.DoesNotContain("RebuildNavigation", source, StringComparison.Ordinal);
    Assert.DoesNotContain("Items.Clear", source, StringComparison.Ordinal);
    Assert.Contains("content.Title = contentLabel", source, StringComparison.Ordinal);
    Assert.Contains("section.Title = contentLabel", source, StringComparison.Ordinal);
    Assert.Contains("Navigation.NavigationStack", source, StringComparison.Ordinal);
  }

  [Fact]
  public void EveryLocalizedValueBindingObservesTheLocaleVersion()
  {
    var appRoot = RepoPath("dotnet-clients", "src", "Voucha.Client.App");
    var xaml = Directory.EnumerateFiles(appRoot, "*.xaml", SearchOption.AllDirectories)
        .Select(File.ReadAllText)
        .ToArray();

    Assert.DoesNotContain(
        xaml,
        source => source.Contains(
            "Converter={StaticResource UiLocalizedValue}",
            StringComparison.Ordinal));
    Assert.Contains(
        xaml,
        source => source.Contains("{app:UiLocalizedValue ", StringComparison.Ordinal));
    var extension = ReadAppSource("UiLocalizedValueExtension.cs");
    var version = ReadAppSource("UiLocaleVersion.cs");
    Assert.Contains("new MultiBinding", extension, StringComparison.Ordinal);
    Assert.Contains("nameof(UiLocaleVersion.Version)", extension, StringComparison.Ordinal);
    Assert.Contains("SubscribeLocaleChanges(this)", version, StringComparison.Ordinal);
    Assert.Contains("OnPropertyChanged()", version, StringComparison.Ordinal);
  }

  [Fact]
  public void LocalizedValueMarkupRequiresAnExplicitFormatMode()
  {
    var appRoot = RepoPath("dotnet-clients", "src", "Voucha.Client.App");
    var xaml = Directory.EnumerateFiles(appRoot, "*.xaml", SearchOption.AllDirectories)
        .Select(File.ReadAllText)
        .ToArray();
    var extension = ReadAppSource("UiLocalizedValueExtension.cs");

    Assert.DoesNotContain(
        xaml,
        source => source.Split("{app:UiLocalizedValue ", StringSplitOptions.None)
            .Skip(1)
            .Any(binding => !binding[..binding.IndexOf('}')].Contains("Format=", StringComparison.Ordinal)));
    Assert.Contains("ArgumentException.ThrowIfNullOrWhiteSpace(format)", extension, StringComparison.Ordinal);
  }

  [Fact]
  public void PlatformLanguageProviderReadsOrderedNativePreferences()
  {
    var source = ReadAppSource("MauiDeviceLanguageProvider.cs");

    Assert.Contains("NSLocale.PreferredLanguages", source, StringComparison.Ordinal);
    Assert.Contains("LocaleList.GetAdjustedDefault", source, StringComparison.Ordinal);
    Assert.Contains("GlobalizationPreferences.Languages", source, StringComparison.Ordinal);
    Assert.Contains("DeviceLanguagePreferences.Ordered", source, StringComparison.Ordinal);
  }

  [Fact]
  public void PostDetailBindingsUseLocalizedTitlesAndRefreshVisibleRows()
  {
    var xaml = ReadAppSource("Pages/PostDetailPage.xaml");
    var binding = ReadAppSource("Pages/PostDetailPageBinding.cs");
    var row = ReadAppSource("Pages/PostDetailPageRow.cs");

    Assert.Contains("{Binding RootRow.LocalizedTitle}", xaml, StringComparison.Ordinal);
    Assert.Contains("{Binding LocalizedTitle}", xaml, StringComparison.Ordinal);
    Assert.DoesNotContain("{Binding RootRow.Post.Title}", xaml, StringComparison.Ordinal);
    Assert.DoesNotContain("{Binding Post.Title}", xaml, StringComparison.Ordinal);
    Assert.Contains("SubscribeLocaleChanges(this)", binding, StringComparison.Ordinal);
    Assert.Contains("OnUiLocaleChanged() => RefreshRows()", binding, StringComparison.Ordinal);
    Assert.Contains("UiTaxonomy.PostType(ProtocolPostType)", row, StringComparison.Ordinal);
  }

  [Fact]
  public void LandingPageAnalyticsMetricLabelsUseDynamicResources()
  {
    var listPage = ReadAppSource("Pages/LandingPagesPage.xaml");
    var analyticsPage = ReadAppSource("Pages/LandingPageAnalyticsPage.xaml");

    Assert.DoesNotContain("Text=\"CTR\"", listPage, StringComparison.Ordinal);
    Assert.DoesNotContain("Text=\"CTR\"", analyticsPage, StringComparison.Ordinal);
    Assert.Contains(
        "Text=\"{DynamicResource native.dotnet.landingPages.ctr}\"",
        listPage,
        StringComparison.Ordinal);
    Assert.Contains(
        "Text=\"{DynamicResource native.dotnet.landingPages.ctr}\"",
        analyticsPage,
        StringComparison.Ordinal);
  }

  private static string ReadCoreTypeSources(string relativePath)
  {
    var path = RepoPath(
        "dotnet-clients",
        "src",
        "Voucha.Client.Core",
        relativePath.Replace('/', Path.DirectorySeparatorChar));
    var directory = Path.GetDirectoryName(path)!;
    var stem = Path.GetFileNameWithoutExtension(path);
    return string.Concat(
        Directory.EnumerateFiles(directory, $"{stem}*.cs").Select(File.ReadAllText));
  }

  private static string ReadAppSource(string relativePath) =>
      File.ReadAllText(RepoPath(
          "dotnet-clients",
          "src",
          "Voucha.Client.App",
          relativePath.Replace('/', Path.DirectorySeparatorChar)));

  private static string RepoPath(params string[] parts)
  {
    var starts = new[]
    {
      Environment.GetEnvironmentVariable("PWD"),
      Environment.GetEnvironmentVariable("GITHUB_WORKSPACE"),
      Directory.GetCurrentDirectory(),
      Uri.UnescapeDataString(AppContext.BaseDirectory),
    }.Where(start => !string.IsNullOrWhiteSpace(start));
    foreach (var start in starts)
    {
      var directory = new DirectoryInfo(start!);
      while (directory is not null)
      {
        var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
        if (File.Exists(candidate) || Directory.Exists(candidate)) return candidate;
        directory = directory.Parent;
      }
    }

    throw new DirectoryNotFoundException("Could not find repository root.");
  }
}

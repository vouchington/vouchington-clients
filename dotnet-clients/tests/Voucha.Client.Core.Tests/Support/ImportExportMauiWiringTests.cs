using Xunit;

namespace Voucha.Client.Core.Tests.Support;

public sealed class ImportExportMauiWiringTests
{
  [Fact]
  public void NativePageAndAdaptersUsePlatformFileAndShareControls()
  {
    var page = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Pages", "ImportExportPage.xaml"));
    var pageCode = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Pages", "ImportExportPage.xaml.cs"));
    var adapter = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Support", "ImportExportFileAdapter.cs"));
    var presenter = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Support", "ImportExportSharePresenter.cs"));

    Assert.Contains("<Editor", page, StringComparison.Ordinal);
    Assert.Contains("<ProgressBar", page, StringComparison.Ordinal);
    Assert.Contains("FilePicker.Default.PickAsync", adapter, StringComparison.Ordinal);
    Assert.Contains("presenter.ShareAsync", adapter, StringComparison.Ordinal);
    Assert.Contains("ExportShareFileStager.StageAndUseAsync", adapter, StringComparison.Ordinal);
    Assert.DoesNotContain("presenter.ShareAsync(document.FilePath", adapter, StringComparison.Ordinal);
    Assert.Contains("MainThread.InvokeOnMainThreadAsync", presenter, StringComparison.Ordinal);
    Assert.Contains("Share.Default.RequestAsync", presenter, StringComparison.Ordinal);
    Assert.DoesNotContain("Share.Default", adapter, StringComparison.Ordinal);
    Assert.DoesNotContain("WebView", page + adapter + presenter, StringComparison.Ordinal);
    Assert.DoesNotContain("OpenBrowser", page + adapter + presenter, StringComparison.Ordinal);
    Assert.Contains("IsEnabled=\"{Binding CanImport}\"", page, StringComparison.Ordinal);
    Assert.Contains("IsEnabled=\"{Binding CanExport}\"", page, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding CanCancelActiveOperation}\"", page, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding CanResume}\"", page, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding HasPartialFailures}\"", page, StringComparison.Ordinal);
    Assert.Contains("ItemsSource=\"{Binding PresentationResults}\"", page, StringComparison.Ordinal);
    Assert.Contains("Text=\"{DynamicResource native.swift.importExport.exportJson}\"", page, StringComparison.Ordinal);
    Assert.Contains("Text=\"{DynamicResource native.swift.importExport.exportCsv}\"", page, StringComparison.Ordinal);
    Assert.Contains("Text=\"{DynamicResource native.swift.importExport.exportOpml}\"", page, StringComparison.Ordinal);
    Assert.Contains("ItemsSource=\"{Binding SourceExportFeedTypeOptions}\"", page, StringComparison.Ordinal);
    Assert.Contains("ItemDisplayBinding=\"{Binding DisplayLabel}\"", page, StringComparison.Ordinal);
    Assert.Contains("SelectedItem=\"{Binding SelectedSourceExportFeedTypeOption}\"", page, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding IsTopics}\"", page, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding IsSources}\"", page, StringComparison.Ordinal);
    Assert.Contains("viewModel.CancelActiveOperations();", pageCode, StringComparison.Ordinal);
    Assert.Contains("class ImportExportPage : ContentPage, IDisposable", pageCode, StringComparison.Ordinal);
    Assert.Contains("protected override void OnParentSet()", pageCode, StringComparison.Ordinal);
    Assert.Contains("else if (hadNavigationParent) Dispose();", pageCode, StringComparison.Ordinal);
    Assert.Contains("viewModel.Dispose();", pageCode, StringComparison.Ordinal);
    Assert.Contains("lifecycleCancellation.Token", pageCode, StringComparison.Ordinal);
    Assert.DoesNotContain("CancellationToken.None", pageCode, StringComparison.Ordinal);
  }

  [Fact]
  public void MacCatalystDeclaresAndSelectsTheOpmlExtensionAsXml()
  {
    var adapter = File.ReadAllText(RepoPath(
        "dotnet-clients", "src", "Voucha.Client.App", "Support", "ImportExportFileAdapter.cs"));
    var plist = File.ReadAllText(RepoPath(
        "dotnet-clients", "src", "Voucha.Client.App", "Platforms", "MacCatalyst", "Info.plist"));

    Assert.Contains("\"ai.voucha.opml\"", adapter, StringComparison.Ordinal);
    Assert.Contains("<key>UTExportedTypeDeclarations</key>", plist, StringComparison.Ordinal);
    Assert.Contains("<string>ai.voucha.opml</string>", plist, StringComparison.Ordinal);
    Assert.Contains("<string>public.xml</string>", plist, StringComparison.Ordinal);
    Assert.Contains("<key>public.filename-extension</key>", plist, StringComparison.Ordinal);
    Assert.Contains("<string>opml</string>", plist, StringComparison.Ordinal);
    Assert.DoesNotContain("public.plain-text", adapter, StringComparison.Ordinal);
  }

  [Fact]
  public void MauiBoundViewModelPreservesCapturedSynchronizationContext()
  {
    var directory = RepoPath("dotnet-clients", "src", "Voucha.Client.Core", "ImportExport");
    var source = string.Join('\n', Directory.GetFiles(directory, "ImportExportViewModel*.cs").Select(File.ReadAllText));

    Assert.DoesNotContain("ConfigureAwait(false)", source, StringComparison.Ordinal);
    Assert.Contains("ConfigureAwait(true)", source, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("TopicsPage.xaml", "OnImportExportClicked")]
  [InlineData("NewsFeedsPage.xaml", "OnImportExportClicked")]
  public void ExistingNativePagesExposeImportExportActions(string file, string handler)
  {
    var source = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Pages", file));
    Assert.Contains("Text=\"{DynamicResource native.swift.importExport.title}\"", source, StringComparison.Ordinal);
    Assert.Contains(handler, source, StringComparison.Ordinal);
  }

  private static string RepoPath(params string[] parts)
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
      var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
      if (File.Exists(candidate) || Directory.Exists(candidate)) return candidate;
      directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not find repository root.");
  }
}

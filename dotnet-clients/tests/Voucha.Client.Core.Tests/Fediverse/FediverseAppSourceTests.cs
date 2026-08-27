using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.Core.Tests.Fediverse;

public sealed class FediverseAppSourceTests
{
  [Fact]
  public void ProviderControlsExposeNativeAccessibleSelectionState()
  {
    var xaml = Source("Pages", "OmnisearchPage.xaml");
    Assert.Equal(5, Count(xaml, "GroupName=\"FediverseProviders\""));
    Assert.Equal(5, Count(xaml, "<RadioButton"));
    Assert.Equal(5, Count(xaml, "Mode=OneWay"));
    var providers = new[]
    {
      (Binding: "AllProviders", Automation: "All", Content: "{DynamicResource native.swift.routeSurface.allProviders}"),
      (Binding: "PeerTube", Automation: "PeerTube", Content: "PeerTube"),
      (Binding: "Mastodon", Automation: "Mastodon", Content: "Mastodon"),
      (Binding: "Lemmy", Automation: "Lemmy", Content: "Lemmy"),
      (Binding: "Bluesky", Automation: "Bluesky", Content: "Bluesky"),
    };
    foreach (var provider in providers)
    {
      Assert.Contains($"AutomationId=\"FediverseProvider{provider.Automation}\"", xaml, StringComparison.Ordinal);
      Assert.Contains($"Content=\"{provider.Content}\"", xaml, StringComparison.Ordinal);
      Assert.Contains($"IsChecked=\"{{Binding Is{provider.Binding}Selected, Mode=OneWay}}\"", xaml, StringComparison.Ordinal);
    }
    Assert.DoesNotContain("IsEnabled=\"{Binding IsPeerTube", xaml, StringComparison.Ordinal);
    var code = Source("Pages", "OmnisearchPage.xaml.cs");
    Assert.Contains("FediverseProviderSelection.ShouldRoute", code, StringComparison.Ordinal);
    Assert.Contains("viewModel.SelectedFediverseProvider", code, StringComparison.Ordinal);
  }

  [Fact]
  public void BlueskyCallbacksNavigateColdOrWarmAppsToObservableSettings()
  {
    var links = Source("AppShell.AppLinks.cs");
    var settings = Source("Pages", "SettingsPage.Bluesky.cs");
    var lifecycle = Source("Pages", "SettingsPage.AccountData.cs");
    Assert.Contains("HandleBlueskyCallbackAsync(url)", links, StringComparison.Ordinal);
    Assert.Contains("OpenIntentAsync(NavigationCatalog.SettingsIntentId)", links, StringComparison.Ordinal);
    Assert.Contains("blueskyCoordinator.PropertyChanged += OnBlueskyStateChanged", settings, StringComparison.Ordinal);
    Assert.Contains("sessionStore.SessionChanged += OnBlueskySessionChanged", settings, StringComparison.Ordinal);
    Assert.Contains("MainThread.BeginInvokeOnMainThread(RefreshBlueskyPresentation)", settings, StringComparison.Ordinal);
    Assert.Contains("AttachBlueskyObservers();", lifecycle, StringComparison.Ordinal);
    Assert.Contains("DetachBlueskyObservers();", lifecycle, StringComparison.Ordinal);
  }

  [Fact]
  public void SettingsRendersLinkedUnlinkedAndUnlinkLifecycleStates()
  {
    var xaml = Source("Pages", "SettingsPage.xaml");
    var code = Source("Pages", "SettingsPage.Bluesky.cs");
    Assert.Contains("x:Name=\"BlueskyLinkedHandle\"", xaml, StringComparison.Ordinal);
    Assert.Contains("x:Name=\"BlueskyConnectButton\"", xaml, StringComparison.Ordinal);
    Assert.Contains("x:Name=\"BlueskyDisconnectButton\"", xaml, StringComparison.Ordinal);
    Assert.Contains("blueskyCoordinator.IsLinked", code, StringComparison.Ordinal);
    Assert.Contains("NativeSwiftSettingsBlueskyDisconnectConfirmation", code, StringComparison.Ordinal);
    Assert.Contains("NativeSwiftSettingsBlueskyDisconnecting", code, StringComparison.Ordinal);
    Assert.Contains("NativeSwiftSettingsBlueskyDisconnectFailed", code, StringComparison.Ordinal);
    Assert.Contains("BlueskyStatus.Text = string.Empty", code, StringComparison.Ordinal);
    Assert.Contains("NativeBlueskyLinkState.Connected", code, StringComparison.Ordinal);
  }

  [Fact]
  public void SettingsHandlesApiConnectFailuresAndGatesLifecycleActions()
  {
    var code = Source("Pages", "SettingsPage.Bluesky.cs");
    Assert.Contains(
        "ex is HttpRequestException or InvalidOperationException or VouchaApiException",
        code,
        StringComparison.Ordinal);
    foreach (var state in new[] { "Idle", "Cancelled", "Expired", "Failed" })
    {
      Assert.Contains($"NativeBlueskyLinkState.{state}", code, StringComparison.Ordinal);
    }
    Assert.Contains(
        "BlueskyCancelButton.IsVisible = blueskyCoordinator.State == NativeBlueskyLinkState.WaitingForCallback",
        code,
        StringComparison.Ordinal);
  }

  [Fact]
  public void DirectoryPageBindsAllStatesPaginationAndNativeRowNavigation()
  {
    var xaml = Source("Pages", "FediverseInstancesPage.xaml");
    var code = Source("Pages", "FediverseInstancesPage.xaml.cs");
    Assert.Contains("ItemsSource=\"{Binding Items}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("CollectionView.EmptyView", xaml, StringComparison.Ordinal);
    Assert.Contains("Text=\"{Binding ErrorMessage}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("IsRunning=\"{Binding IsLoading}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding CanLoadMore}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("await viewModel.LoadMoreAsync()", code, StringComparison.Ordinal);
    Assert.Contains("/instance/{Uri.EscapeDataString(row.Slug)}", code, StringComparison.Ordinal);
  }

  [Fact]
  public void DirectoryRoutesPreserveInitialQueryForColdAndWarmPages()
  {
    var shell = Source("AppShell.FediverseIntentPages.cs");
    var page = Source("Pages", "FediverseInstancesPage.xaml.cs");
    Assert.Equal(2, Count(shell, "CreateFediverseInstancesPage(match)"));
    Assert.Contains("match?.QueryValue(\"q\", \"query\")", shell, StringComparison.Ordinal);
    Assert.Contains("page.SetInitialQuery", shell, StringComparison.Ordinal);
    Assert.Contains("Query.Text = initialQuery", page, StringComparison.Ordinal);
    Assert.Contains("viewModel.LoadAsync(initialQuery)", page, StringComparison.Ordinal);
    Assert.Contains("initialQuery = Query.Text", page, StringComparison.Ordinal);
  }

  [Fact]
  public void InstancePageRoutesActionsThroughHydratedTopicOwner()
  {
    var xaml = Source("Pages", "FediverseInstanceDetailPage.xaml");
    var code = Source("Pages", "FediverseInstanceDetailPage.xaml.cs");
    Assert.Contains("Actions.TopicFollowActionLabel", xaml, StringComparison.Ordinal);
    Assert.Contains("Actions.TopicMuteActionLabel", xaml, StringComparison.Ordinal);
    Assert.Contains("SemanticVoteActionSheet.ChooseSentimentAsync", code, StringComparison.Ordinal);
    Assert.Contains("viewModel.Actions.VoteTopicAsync(null)", code, StringComparison.Ordinal);
    Assert.Contains("viewModel.Actions.ToggleTopicFollowAsync()", code, StringComparison.Ordinal);
    Assert.Contains("viewModel.Actions.ToggleTopicMuteAsync()", code, StringComparison.Ordinal);
    Assert.Contains("Unloaded += OnUnloaded", code, StringComparison.Ordinal);
    Assert.Contains("Unloaded -= OnUnloaded", code, StringComparison.Ordinal);
    Assert.Contains("viewModel.Dispose();", code, StringComparison.Ordinal);
  }

  private static int Count(string value, string text) =>
      value.Split(text, StringSplitOptions.None).Length - 1;

  private static string Source(params string[] parts)
  {
    foreach (var start in new[] { Path.GetDirectoryName(SourceFile())!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
      for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
      {
        var app = Path.Combine(directory.FullName, "dotnet-clients", "src", "Voucha.Client.App");
        if (Directory.Exists(app)) return File.ReadAllText(Path.GetFullPath(Path.Combine(app, Path.Combine(parts))));
      }
    }
    throw new DirectoryNotFoundException("Could not find the .NET app source directory.");
  }

  private static string SourceFile([CallerFilePath] string sourceFile = "") => sourceFile;
}

using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

[Trait("Category", "ParityEvidence")]
public sealed class ModerationReportsPageSourceTests
{
  [Fact]
  public void ReportsPageRendersLoadingState()
  {
    var cards = CardsSource();

    Assert.Contains("if (viewModel.IsLoading", cards, StringComparison.Ordinal);
    Assert.Contains("new ActivityIndicator { IsRunning = true }", cards, StringComparison.Ordinal);
  }

  [Fact]
  public void ReportsPageRendersEmptyState()
  {
    var cards = CardsSource();

    Assert.Contains("if (queue.Children.Count == 0)", cards, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationReportsNoReportsMessage", cards, StringComparison.Ordinal);
  }

  [Fact]
  public void ReportsPageRendersErrorState()
  {
    var root = ClientRepositoryRoot();
    var page = Source(root, "dotnet-clients/src/Voucha.Client.App/Pages/ModerationReportsPage.cs");
    var cards = CardsSource();

    Assert.Contains("errorLabel,", page, StringComparison.Ordinal);
    Assert.Contains("errorLabel.Text = viewModel.ErrorMessage;", cards, StringComparison.Ordinal);
  }

  [Fact]
  public void BulkActionsRequireConfirmationBeforeMutation()
  {
    var actions = Source(
        ClientRepositoryRoot(), "dotnet-clients/src/Voucha.Client.App/Pages/ModerationReportsPage.Actions.cs");

    var confirmation = actions.IndexOf("DisplayAlertAsync", StringComparison.Ordinal);
    var mutation = actions.IndexOf("await action().ConfigureAwait(true)", StringComparison.Ordinal);
    Assert.True(confirmation >= 0 && mutation > confirmation);
    Assert.Equal(5, actions.Split("ConfirmBulkActionAsync", StringSplitOptions.None).Length - 1);
  }

  [Fact]
  public void GroupedRenderingUsesActiveReportsAndEligibleActions()
  {
    var root = ClientRepositoryRoot();
    var page = Source(root, "dotnet-clients/src/Voucha.Client.App/Pages/ModerationReportsPage.cs");
    var cards = CardsSource();
    var actions = Source(
        root, "dotnet-clients/src/Voucha.Client.App/Pages/ModerationReportsPage.Actions.cs");

    Assert.Contains("viewModel.ReportsForCluster(cluster)", cards, StringComparison.Ordinal);
    Assert.DoesNotContain("foreach (var report in cluster.Reports)", cards, StringComparison.Ordinal);
    Assert.Contains("DuplicateClusters.Where(viewModel.HasActiveReports)", cards, StringComparison.Ordinal);
    Assert.Contains("viewModel.PresentationCounts(duplicate)", cards, StringComparison.Ordinal);
    Assert.Contains("viewModel.PresentationReportCount(cluster)", cards, StringComparison.Ordinal);
    Assert.Contains("viewModel.PresentationReasons", cards, StringComparison.Ordinal);
    Assert.Contains("viewModel.CanDismissCluster(clusterId)", actions, StringComparison.Ordinal);
    Assert.Contains("viewModel.CanRemoveClusterTargets(clusterId)", actions, StringComparison.Ordinal);
    Assert.Contains("viewModel.Mode == ModerationReportsMode.Flat", page + cards, StringComparison.Ordinal);
    Assert.Contains("!viewModel.IsQueueInteractionBlocked", cards + actions, StringComparison.Ordinal);
  }

  [Fact]
  public void MauiReportsRouteUsesDedicatedNativeTriageSurface()
  {
    var root = ClientRepositoryRoot();
    var route = Source(root, "dotnet-clients/src/Voucha.Client.App/AppShell.ModerationRoutes.cs");
    var page = Source(root, "dotnet-clients/src/Voucha.Client.App/Pages/ModerationReportsPage.cs");
    var cards = Source(root, "dotnet-clients/src/Voucha.Client.App/Pages/ModerationReportsPage.Cards.cs");
    var presentation = Source(root, "dotnet-clients/src/Voucha.Client.App/Pages/ModerationReportsPage.Presentation.cs");
    var actions = Source(root, "dotnet-clients/src/Voucha.Client.App/Pages/ModerationReportsPage.Actions.cs");

    Assert.Contains("GetRequiredService<ModerationReportsPage>", route, StringComparison.Ordinal);
    Assert.Contains("foreach (var content in EnumerateShellContents(\"moderation\"))", route, StringComparison.Ordinal);
    Assert.Contains("content.Content as ModerationReportsPage", route, StringComparison.Ordinal);
    Assert.Contains(
        "ModerationDisputesPageLifetime.Replace(content, reportsPage)",
        route,
        StringComparison.Ordinal);
    Assert.Contains("await reportsPage.ReloadAsync()", route, StringComparison.Ordinal);
    Assert.Contains(
        "ModerationDisputesPageLifetime.Replace(content, moderationPage)",
        route,
        StringComparison.Ordinal);
    Assert.Contains("await moderationPage.ApplyContextAsync(context)", route, StringComparison.Ordinal);
    Assert.Contains("ModerationReportsViewModel", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationReportsReporter", cards, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationReportsAiJudgement", presentation, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeDotnetModerationRebasedBanEvasionDetail", presentation, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationReportsDismissLoadedReports", actions, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationReportsRemoveLoadedPosts", actions, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeDotnetModerationRebasedStaffQueueDescription", page, StringComparison.Ordinal);
    var filterCall = page.IndexOf("await viewModel.SetFiltersAsync", StringComparison.Ordinal);
    var filterCancellationCatch = page.IndexOf("catch (OperationCanceledException)", filterCall, StringComparison.Ordinal);
    var loadMore = page.IndexOf("private async Task LoadMoreAsync", StringComparison.Ordinal);
    Assert.True(filterCall >= 0 && filterCancellationCatch > filterCall && filterCancellationCatch < loadMore);
    Assert.Contains("content.Children.Add(ClusterActions(duplicate.Id))", cards, StringComparison.Ordinal);
    Assert.Contains("duplicate.Clusters.Where(item => viewModel.ReportsForCluster(item).Count > 0)", cards, StringComparison.Ordinal);
    Assert.DoesNotContain("WebView", page + cards + actions, StringComparison.Ordinal);
  }

  [Fact]
  public void ModePickerUsesTypedOptionsAndSafeMapping()
  {
    var root = ClientRepositoryRoot();
    var page = Source(root, "dotnet-clients/src/Voucha.Client.App/Pages/ModerationReportsPage.cs");
    var filters = Source(root, "dotnet-clients/src/Voucha.Client.App/Pages/ModerationReportsPage.Filters.cs");

    Assert.Contains("record ModeOption(string Label, ModerationReportsMode Mode)", page, StringComparison.Ordinal);
    Assert.Contains("modePicker.ItemsSource = ModeOptions();", page, StringComparison.Ordinal);
    Assert.Contains("modePicker.ItemDisplayBinding", page, StringComparison.Ordinal);
    Assert.Contains("ModeOptions()[modePicker.SelectedIndex].Mode", filters, StringComparison.Ordinal);
    Assert.Contains("modePicker.SelectedIndex < ModeOptions().Length", filters, StringComparison.Ordinal);
    Assert.DoesNotContain("modePicker.SelectedIndex = (int)viewModel.Mode", page + filters, StringComparison.Ordinal);
  }

  private static string Source(string root, string relativePath) =>
      File.ReadAllText(Path.Combine(root, relativePath));

  private static string CardsSource() => Source(
      ClientRepositoryRoot(), "dotnet-clients/src/Voucha.Client.App/Pages/ModerationReportsPage.Cards.cs");

  private static string ClientRepositoryRoot([CallerFilePath] string sourcePath = "")
  {
    DirectoryInfo? directory = new(Path.GetDirectoryName(sourcePath)!);
    while (directory is not null)
    {
      if (File.Exists(Path.Combine(directory.FullName, "dotnet-clients", "Voucha.DotNet.sln")))
        return directory.FullName;
      directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not find client repository root from source path.");
  }
}

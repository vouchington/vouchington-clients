using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationAppealsPageSourceTests
{
  [Fact]
  public void DedicatedStaffPageExposesFiltersLifecycleActionsAndReadOnlyStaffContext()
  {
    var page = Source("ModerationAppealsPage.cs") +
        Source("ModerationAppealCardView.cs") +
        Source("ModerationAppealCardPresentation.cs") +
        Source("ModerationAppealCardPresentation.Context.cs");
    Assert.Contains("ModerationAppealStatus.Pending", page, StringComparison.Ordinal);
    Assert.Contains("ModerationAppealStatus.Resolved", page, StringComparison.Ordinal);
    Assert.Contains("ModerationAppealStatus.Dismissed", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsPublicResponse", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.CommonSave", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsApprove", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsSend", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsRerunAi", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsAccept", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsReduce", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsDeny", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsInternalAiResponse", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsInternalNotes", page, StringComparison.Ordinal);
    Assert.DoesNotContain("Placeholder = \"Internal notes\"", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsDecisionContext", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsCommunityContext", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsPostContext", page, StringComparison.Ordinal);
    Assert.Contains("StaffContext", page, StringComparison.Ordinal);
  }

  [Fact]
  public void AuthorizedPageRendersBoundLoadingAndSuccessfulEmptyStates()
  {
    var page = Source("ModerationAppealsPage.cs");
    Assert.Contains("ActivityIndicator", page, StringComparison.Ordinal);
    Assert.Contains("nameof(ModerationAppealsViewModel.IsLoading)", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsNoAppealsMatch", page, StringComparison.Ordinal);
    Assert.Contains("nameof(ModerationAppealsViewModel.IsEmpty)", page, StringComparison.Ordinal);
  }

  [Fact]
  public void DisappearingPageCancelsDisposesAndClearsItsLoadTokenSource()
  {
    var page = Source("ModerationAppealsPage.cs");
    Assert.Contains("loadCancellation?.Cancel();", page, StringComparison.Ordinal);
    Assert.Contains("loadCancellation?.Dispose();", page, StringComparison.Ordinal);
    Assert.Contains("loadCancellation = null;", page, StringComparison.Ordinal);
  }

  [Fact]
  public void AppealCardsRenderDraftApprovedAndSentLifecycleTimestamps()
  {
    var card = Source("ModerationAppealCardView.cs") + Source("ModerationAppealCardPresentation.cs");
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsDrafted", card, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsApproved", card, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsSent", card, StringComparison.Ordinal);
    Assert.Contains("ApprovedAt", card, StringComparison.Ordinal);
    Assert.Contains("SentAt", card, StringComparison.Ordinal);
    Assert.Contains("CreatedAt", card, StringComparison.Ordinal);
    Assert.Contains("UpdatedAt", card, StringComparison.Ordinal);
    Assert.Contains("IsOverdue", card, StringComparison.Ordinal);
    Assert.Contains("ResolvedAt", card, StringComparison.Ordinal);
    Assert.Contains("ResolvedById", card, StringComparison.Ordinal);
    Assert.Contains("ResolutionAction", card, StringComparison.Ordinal);
    Assert.Contains("UiCopy.FormatDateTime", card, StringComparison.Ordinal);
  }

  [Fact]
  public void AppealCardsSubscribeOnlyWhileLoadedAndRepeatedLoadsStayIdempotent()
  {
    var card = Source("ModerationAppealCardView.cs");
    Assert.Contains("Loaded += OnLoaded;", card, StringComparison.Ordinal);
    Assert.Contains("Unloaded += OnUnloaded;", card, StringComparison.Ordinal);
    Assert.Contains("private void OnLoaded(object? sender, EventArgs args)", card, StringComparison.Ordinal);
    Assert.Contains("private void OnUnloaded(object? sender, EventArgs args)", card, StringComparison.Ordinal);
    Assert.Contains(
        "viewModel.PropertyChanged -= ViewModelChanged;\n    viewModel.PropertyChanged += ViewModelChanged;",
        card,
        StringComparison.Ordinal
    );
    Assert.Equal(2, Count(card, "viewModel.PropertyChanged -= ViewModelChanged;"));
    Assert.Equal(1, Count(card, "viewModel.PropertyChanged += ViewModelChanged;"));
  }

  [Fact]
  public void AuthenticatedNonStaffViewerReceivesTheStaffAccessMessage()
  {
    var page = Source("ModerationAppealsPage.cs");
    Assert.Contains("if (!viewModel.CanAccess)", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftModerationAppealsStaffAccessMessage", page, StringComparison.Ordinal);
  }

  [Fact]
  public void RoutingKeepsPersonalAppealsOnTheGenericReadOnlySurface()
  {
    var routing = SourceFromAppRoot("AppShell.ModerationRoutes.cs");
    Assert.Contains("context.RouteKind == ModerationRouteKind.Appeals && !context.Mine", routing, StringComparison.Ordinal);
    Assert.Contains("GetRequiredService<ModerationAppealsPage>()", routing, StringComparison.Ordinal);
    Assert.Contains("GetRequiredService<ModerationPage>()", routing, StringComparison.Ordinal);
    Assert.Contains(
        "ModerationDisputesPageLifetime.Replace(content, appealsPage)",
        routing,
        StringComparison.Ordinal);
    Assert.Contains("appealsPage.ReloadAsync()", routing, StringComparison.Ordinal);
    Assert.Contains(
        "ModerationDisputesPageLifetime.Replace(content, moderationPage)",
        routing,
        StringComparison.Ordinal);
    Assert.Contains("ApplyContextAsync(context)", routing, StringComparison.Ordinal);
  }

  private static string Source(string file) => SourceFromAppRoot(Path.Combine("Pages", file));

  private static int Count(string source, string value) =>
      source.Split(value, StringSplitOptions.None).Length - 1;

  private static string SourceFromAppRoot(
      string relativePath,
      [CallerFilePath] string sourcePath = "")
  {
    var directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath)!);
    while (directory is not null)
    {
      var candidate = Path.Combine(directory.FullName, "dotnet-clients", "src", "Voucha.Client.App", relativePath);
      if (File.Exists(candidate)) return File.ReadAllText(candidate);
      directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not find the .NET app source directory.");
  }
}

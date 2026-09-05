using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostDetailPageReplyDraftWiringTests
{
  [Fact]
  public void ReplyAndQuoteDraftsSurviveACancelledTurnstileChallengeUntilTheirMutationRefreshesRows()
  {
    var actions = AppSource("PostDetailPage.Actions.cs");
    var drafts = AppSource("PostDetailPage.ReplyDraft.cs");

    Assert.Contains("private ReplyDraft? pendingReplyDraft", drafts, StringComparison.Ordinal);
    Assert.Contains("draft.ParentPostId == row.Id && draft.IsQuote == isQuote", drafts, StringComparison.Ordinal);
    Assert.Contains("new ReplyDraft(row.Id, isQuote, markdown)", drafts, StringComparison.Ordinal);
    Assert.Contains("draft.ParentPostId == parentPostId && draft.IsQuote == isQuote", drafts, StringComparison.Ordinal);

    AssertDraftOutlivesChallenge(actions, "OnReplyClicked", "isQuote: false");
    AssertDraftOutlivesChallenge(actions, "OnQuoteClicked", "isQuote: true");
  }

  private static void AssertDraftOutlivesChallenge(string actions, string action, string mode)
  {
    var section = actions.Split($"private async void {action}", 2)[1]
        .Split("private async void", 2)[0];

    Assert.True(section.IndexOf("ShowReplyEditorAsync", StringComparison.Ordinal) <
        section.IndexOf("GetTurnstileTokenAsync", StringComparison.Ordinal));
    Assert.True(section.IndexOf("if (!created) return;", StringComparison.Ordinal) <
        section.IndexOf("binding.RefreshRows()", StringComparison.Ordinal));
    Assert.True(section.IndexOf("binding.RefreshRows()", StringComparison.Ordinal) <
        section.IndexOf($"ClearReplyDraft(row.Id, {mode})", StringComparison.Ordinal));
  }

  private static string AppSource(string file)
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
      var candidate = Path.Combine(directory.FullName, "dotnet-clients", "src", "Voucha.Client.App", "Pages", file);
      if (File.Exists(candidate)) return File.ReadAllText(candidate);
      directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not find the native app source from test output directory.");
  }
}

namespace Voucha.Client.App.Pages;

public partial class PostDetailPage
{
  private ReplyDraft? pendingReplyDraft;

  private async Task<string?> ShowReplyEditorAsync(
      PostDetailPageRow row,
      bool isQuote,
      string title,
      string accept,
      string initialMarkdown)
  {
    var initial = pendingReplyDraft is { } draft && draft.ParentPostId == row.Id && draft.IsQuote == isQuote
        ? draft.Markdown
        : initialMarkdown;
    var markdown = await ShowMarkdownEditorAsync(title, accept, initial).ConfigureAwait(true);
    if (!string.IsNullOrWhiteSpace(markdown))
    {
      pendingReplyDraft = new ReplyDraft(row.Id, isQuote, markdown);
    }
    return markdown;
  }

  private void ClearReplyDraft(string parentPostId, bool isQuote)
  {
    if (pendingReplyDraft is { } draft && draft.ParentPostId == parentPostId && draft.IsQuote == isQuote)
    {
      pendingReplyDraft = null;
    }
  }

  private sealed record ReplyDraft(string ParentPostId, bool IsQuote, string Markdown);
}

using Microsoft.Maui.ApplicationModel;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.App.Controls;
namespace Voucha.Client.App.Pages;

public partial class PostDetailPage
{
  private static PostDetailPageRow RowFrom(object? sender) =>
      sender is Button { CommandParameter: PostDetailPageRow row }
          ? row
          : throw new InvalidOperationException("Missing row binding.");

  private void OnCollapseClicked(object? sender, EventArgs e) => binding.ToggleCollapse(RowFrom(sender).Id);

  private async void OnChooseVoteClicked(object? sender, EventArgs e)
  {
    var row = RowFrom(sender);
    if (!sessionStore.Current.CanCastPublicVotes() || !row.CanCreateVote) return;
    await RunMutationAsync(async () =>
    {
      var choice = string.Equals(row.ProtocolPostType, "topic_recommendation", StringComparison.Ordinal)
          ? await SemanticVoteActionSheet.ChooseRecommendationAsync(this)
          : await SemanticVoteActionSheet.ChooseSentimentAsync(this, row.CurrentVoteChoice);
      if (choice is null) return;
      await binding.ViewModel.VotePostAsync(row.Id, choice);
      if (await emailRecovery.PresentIfRequestedAsync(this, binding.ViewModel.EmailVerificationGate))
      {
        return;
      }
      binding.RefreshRows();
    });
  }

  private async void OnClearVoteClicked(object? sender, EventArgs e)
  {
    var row = RowFrom(sender);
    if (!sessionStore.Current.CanClearPublicVote(row.CurrentVoteChoice) || !row.CanClearVote) return;
    await RunMutationAsync(async () =>
    {
      await binding.ViewModel.VotePostAsync(row.Id, null);
      if (await emailRecovery.PresentIfRequestedAsync(this, binding.ViewModel.EmailVerificationGate))
      {
        return;
      }
      binding.RefreshRows();
    });
  }

  private async void OnSaveClicked(object? sender, EventArgs e)
  {
    if (!await EnsureSignedInAsync()) return;
    var row = RowFrom(sender);
    await RunMutationAsync(async () =>
    {
      await binding.ViewModel.ToggleSavePostAsync(row.Id, row.IsSaved);
      binding.RefreshRows();
    });
  }

  private async void OnReplyClicked(object? sender, EventArgs e)
  {
    if (!await EnsureSignedInAsync()) return;
    var row = RowFrom(sender);
    var markdown = await ShowMarkdownEditorAsync(
        UiCopy.Localize(UiMessageKey.NativeSwiftCommentThreadActionRowReply),
        UiCopy.Localize(UiMessageKey.NativeSwiftCommentThreadActionRowReply),
        string.Empty);
    if (string.IsNullOrWhiteSpace(markdown)) return;
    await RunMutationAsync(async () =>
    {
      await binding.ViewModel.ReplyAsync(row.Id, markdown.Trim(), false);
      binding.RefreshRows();
    });
  }

  private async void OnQuoteClicked(object? sender, EventArgs e)
  {
    if (!await EnsureSignedInAsync()) return;
    var row = RowFrom(sender);
    var markdown = await ShowMarkdownEditorAsync(
        UiCopy.Localize(UiMessageKey.NativeSwiftCommentThreadActionRowQuote),
        UiCopy.Localize(UiMessageKey.NativeSwiftCommentThreadActionRowReply),
        BuildQuoteMarkdown(row));
    if (string.IsNullOrWhiteSpace(markdown)) return;
    await RunMutationAsync(async () =>
    {
      await binding.ViewModel.ReplyAsync(row.Id, markdown.Trim(), false);
      binding.RefreshRows();
    });
  }

  private async void OnCopyPermalinkTextClicked(object? sender, EventArgs e) =>
      await Clipboard.Default.SetTextAsync(RowFrom(sender).PermalinkText);

  private async void OnCopyPermalinkPathClicked(object? sender, EventArgs e) =>
      await Clipboard.Default.SetTextAsync(RowFrom(sender).PermalinkPath);

  private async void OnReportClicked(object? sender, EventArgs e)
  {
    if (!await EnsureSignedInAsync()) return;
    var row = RowFrom(sender);
    var reason = await DisplayPromptAsync(
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsReport),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsReason),
        accept: UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsContinue),
        cancel: UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel));
    if (string.IsNullOrWhiteSpace(reason)) return;
    var note = await DisplayPromptAsync(
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsReport),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsNote),
        initialValue: string.Empty,
        accept: UiCopy.Localize(UiMessageKey.NativeDotnetCsharpSubmit),
        cancel: UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel));
    if (note is null) return;
    await RunMutationAsync(() => binding.ViewModel.ReportAsync(row.Id, reason.Trim(), string.IsNullOrWhiteSpace(note) ? null : note.Trim()));
  }

  private async void OnEditClicked(object? sender, EventArgs e)
  {
    if (!await EnsureSignedInAsync()) return;
    var row = RowFrom(sender);
    var markdown = await ShowMarkdownEditorAsync(
        UiCopy.Localize(UiMessageKey.NativeSwiftCommonEdit),
        UiCopy.Localize(UiMessageKey.CommonSave),
        row.BodyText);
    if (string.IsNullOrWhiteSpace(markdown)) return;
    await RunMutationAsync(async () =>
    {
      await binding.ViewModel.EditAsync(row.Id, markdown.Trim());
      binding.RefreshRows();
    });
  }

  private async void OnDeleteClicked(object? sender, EventArgs e)
  {
    if (!await EnsureSignedInAsync()) return;
    var row = RowFrom(sender);
    var confirm = await DisplayAlertAsync(
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsDeletePost),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpThisCannotBeUndone),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDelete),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel));
    if (!confirm) return;
    await RunMutationAsync(async () =>
    {
      await binding.ViewModel.DeleteAsync(row.Id);
      binding.RefreshRows();
    });
  }

  private async void OnLockToggleClicked(object? sender, EventArgs e)
  {
    if (!await EnsureSignedInAsync()) return;
    var row = RowFrom(sender);
    await RunMutationAsync(async () =>
    {
      if (row.IsLocked) await binding.ViewModel.UnlockAsync(row.Id);
      else await binding.ViewModel.LockAsync(row.Id);
      binding.RefreshRows();
    });
  }

}

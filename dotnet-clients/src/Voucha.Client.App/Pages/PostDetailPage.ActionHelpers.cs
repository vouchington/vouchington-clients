using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class PostDetailPage
{
  private Task<bool> EnsureSignedInAsync() =>
      this.EnsureSignedInAsync(sessionStore, serviceProvider);

  private Task<string?> ShowMarkdownEditorAsync(
      string title,
      string accept,
      string initialMarkdown) =>
      NativeMarkdownEditorDialog.ShowAsync(
          Navigation,
          title,
          accept,
          initialMarkdown,
          serviceProvider.GetRequiredService<VouchaApiClient>());

  private async Task<string?> GetTurnstileTokenAsync()
  {
    try
    {
      return await serviceProvider.GetRequiredService<ITurnstileTokenProvider>()
          .GetTokenAsync()
          .ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      return null;
    }
    catch (InvalidOperationException ex)
    {
      await DisplayAlertAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsPostActionFailed),
          ex.Message,
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpOk));
      return null;
    }
  }

  private void AttachSessionChanged()
  {
    if (isSessionChangedAttached) return;
    sessionStore.SessionChanged += sessionChangedHandler;
    isSessionChangedAttached = true;
  }

  private void DetachSessionChanged()
  {
    if (!isSessionChangedAttached) return;
    sessionStore.SessionChanged -= sessionChangedHandler;
    isSessionChangedAttached = false;
  }

  private async Task RunMutationAsync(Func<Task> mutation)
  {
    try
    {
      await mutation().ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
      await DisplayAlertAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsPostActionFailed),
          ex.Message,
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpOk));
    }
  }

  private static string BuildQuoteMarkdown(PostDetailPageRow row)
  {
    var lines = (row.BodyText ?? string.Empty).Split('\n');
    var quoted = string.Join('\n', lines.Select(line => $"> {line}"));
    var viewComment = UiCopy.Localize(UiMessageKey.NativeDotnetPostsViewComment);
    return $"{quoted}\n\n[→ {viewComment}]({row.PermalinkPath})\n\n";
  }
}

using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Support;

public sealed partial class StaffSupportThreadViewModel
{
  public static bool CanEditDraft(SupportMessage message)
  {
    ArgumentNullException.ThrowIfNull(message);
    return message.DraftedAt is not null && message.ApprovedAt is null && message.SentAt is null;
  }

  public static bool CanSendDraft(SupportMessage message)
  {
    ArgumentNullException.ThrowIfNull(message);
    return message.DraftedAt is not null && message.ApprovedAt is not null && message.SentAt is null;
  }

  public async Task GenerateDraftAsync(CancellationToken cancellationToken = default)
  {
    if (Thread is null || !CanGenerateDraft || IsWaitingForDraft || IsBusy) return;
    IsWaitingForDraft = true;
    IsBusy = true;
    ErrorMessage = null;
    var knownIds = Messages.Select(message => message.Id).ToHashSet(StringComparer.Ordinal);
    try
    {
      await service.QueueDraftAsync(Thread.Id, cancellationToken).ConfigureAwait(true);
      if (!await WaitForDraftAsync(Thread.Id, knownIds, cancellationToken).ConfigureAwait(true))
        ErrorMessage = localization.Localize(Voucha.Client.Core.Localization.UiMessageKey.NativeSwiftCommonTryAgain);
    }
    catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
    {
      if (!await TryWaitForDraftAsync(Thread.Id, knownIds, cancellationToken).ConfigureAwait(true))
        ErrorMessage = ex.Message;
    }
    catch (Exception ex) when (ex is VouchaApiException or InvalidOperationException or TaskCanceledException)
    {
      ErrorMessage = ex.Message;
    }
    finally { IsWaitingForDraft = false; IsBusy = false; }
  }

  public async Task SaveDraftAsync(SupportMessage message, string bodyText, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(message);
    if (!CanReply || IsBusy) return;
    await RunMutationAsync(async () =>
      ReplaceMessage((await service.UpdateDraftAsync(message.SupportThreadId, message.Id, bodyText, cancellationToken).ConfigureAwait(true)).Message)).ConfigureAwait(true);
  }
  public async Task ApproveAsync(SupportMessage message, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(message);
    if (!CanReply || IsBusy) return;
    await RunMutationAsync(async () =>
      ReplaceMessage((await service.ApproveAsync(message.SupportThreadId, message.Id, cancellationToken).ConfigureAwait(true)).Message)).ConfigureAwait(true);
  }
  public async Task SendAsync(SupportMessage message, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(message);
    if (!CanReply || IsBusy) return;
    var threadId = Thread?.Id;
    await RunMutationAsync(
      async () => ReplaceMessage((await service.SendAsync(message.SupportThreadId, message.Id, cancellationToken).ConfigureAwait(true)).Message),
      threadId is null ? null : () => LoadCoreAsync(threadId, cancellationToken)).ConfigureAwait(true);
  }

  private void ReplaceMessage(SupportMessage replacement) =>
      Messages = Messages.Select(message => message.Id == replacement.Id ? replacement : message).ToArray();

  private async Task<bool> WaitForDraftAsync(string threadId, HashSet<string> knownIds, CancellationToken cancellationToken)
  {
    for (var attempt = 0; attempt < 20; attempt++)
    {
      var response = await service.FetchMessagesAsync(threadId, null, 50, cancellationToken).ConfigureAwait(true);
      if (response.Results.FirstOrDefault(message => message.DraftedAt is not null && !knownIds.Contains(message.Id)) is null)
      {
        if (attempt < 19) await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(true);
        continue;
      }
      var refreshedIds = response.Results.Select(message => message.Id).ToHashSet(StringComparer.Ordinal);
      Messages = [.. Messages.Where(message => !refreshedIds.Contains(message.Id)), .. response.Results];
      return true;
    }
    return false;
  }

  private async Task<bool> TryWaitForDraftAsync(string threadId, HashSet<string> knownIds, CancellationToken cancellationToken)
  {
    try { return await WaitForDraftAsync(threadId, knownIds, cancellationToken).ConfigureAwait(true); }
    catch (Exception ex) when (
        ex is VouchaApiException or HttpRequestException or InvalidOperationException ||
        ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
    {
      return false;
    }
  }
}

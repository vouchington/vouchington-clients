using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Support;

public sealed partial class StaffSupportThreadViewModel(
    IStaffSupportService service,
    string? administratorId,
    IUiLocalization? localization = null) : ObservableObject
{
  private readonly IUiLocalization localization = localization ?? UiLocalization.English;
  private SupportThread? thread;
  private IReadOnlyList<SupportMessage> messages = [];
  private PageInfo? pageInfo;
  private string replyText = string.Empty;
  private bool isBusy;
  private bool isWaitingForDraft;
  private string? errorMessage;

  public SupportThread? Thread { get => thread; private set => SetProperty(ref thread, value); }
  public IReadOnlyList<SupportMessage> Messages
  {
    get => messages;
    private set
    {
      if (SetProperty(ref messages, value)) OnPropertyChanged(nameof(CanGenerateDraft));
    }
  }
  public string ReplyText { get => replyText; set => SetProperty(ref replyText, value); }
  public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
  public bool IsWaitingForDraft { get => isWaitingForDraft; private set => SetProperty(ref isWaitingForDraft, value); }
  public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }
  public bool HasOlderMessages => pageInfo?.HasNextPage == true;
  public bool CanAssign => HasAdministrator && Thread?.Status is SupportThreadStatus.Open;
  public bool CanResolve => HasAdministrator && (Thread?.Status is SupportThreadStatus.Open or SupportThreadStatus.Assigned);
  public bool CanReopen => HasAdministrator && Thread?.Status is SupportThreadStatus.Resolved;
  public bool CanReply => CanResolve;
  public bool CanGenerateDraft =>
      CanReply && !Messages.Any(IsUnsentDraft) && (Messages.Any(IsInbound) || HasOlderMessages);

  public async Task LoadAsync(string threadId, CancellationToken cancellationToken = default)
  {
    if (!HasAdministrator)
    {
      ErrorMessage = localization.Localize(UiMessageKey.NativeSwiftIntegrityAdministratorMessage);
      return;
    }
    if (IsBusy) return;
    IsBusy = true;
    ErrorMessage = null;
    try { await LoadCoreAsync(threadId, cancellationToken).ConfigureAwait(true); }
    catch (Exception ex) when (IsExpectedFailure(ex)) { ErrorMessage = ex.Message; }
    finally { IsBusy = false; }
  }

  public async Task LoadOlderAsync(CancellationToken cancellationToken = default)
  {
    if (Thread is null || pageInfo?.EndCursor is not { } cursor || IsBusy) return;
    await RunMutationAsync(async () =>
    {
      var response = await service.FetchMessagesAsync(Thread.Id, cursor, 50, cancellationToken).ConfigureAwait(true);
      Messages = [.. response.Results.Where(item => Messages.All(existing => existing.Id != item.Id)), .. Messages];
      pageInfo = response.PageInfo;
      OnPropertyChanged(nameof(HasOlderMessages));
      OnPropertyChanged(nameof(CanGenerateDraft));
    }).ConfigureAwait(true);
  }

  public Task AssignAsync(CancellationToken cancellationToken = default) =>
      HasAdministrator
          ? MutateThreadAsync(() => service.AssignAsync(Thread!.Id, administratorId!, cancellationToken))
          : Task.CompletedTask;
  public Task SetResolvedAsync(bool resolved, CancellationToken cancellationToken = default) =>
      MutateThreadAsync(() => service.ResolveAsync(Thread!.Id, resolved, cancellationToken));

  public async Task SaveOutboundReplyAsync(CancellationToken cancellationToken = default)
  {
    if (Thread is null || string.IsNullOrWhiteSpace(ReplyText) || IsBusy) return;
    await RunMutationAsync(async () =>
    {
      var response = await service.CreateMessageAsync(Thread.Id, ReplyText.Trim(), cancellationToken).ConfigureAwait(true);
      Messages = [.. Messages, response.Message];
      ReplyText = string.Empty;
    }).ConfigureAwait(true);
  }

  private async Task MutateThreadAsync(Func<Task<SupportThreadResponse>> action)
  {
    if (Thread is null || IsBusy) return;
    await RunMutationAsync(async () => Thread = (await action().ConfigureAwait(true)).Thread).ConfigureAwait(true);
  }

  private async Task LoadCoreAsync(string threadId, CancellationToken cancellationToken)
  {
    var response = await service.FetchThreadAsync(threadId, cancellationToken).ConfigureAwait(true);
    var messageResponse = await service.FetchMessagesAsync(threadId, null, 50, cancellationToken).ConfigureAwait(true);
    Thread = response.Thread;
    Messages = messageResponse.Results;
    pageInfo = messageResponse.PageInfo;
    OnPropertyChanged(nameof(HasOlderMessages));
    OnPropertyChanged(nameof(CanGenerateDraft));
  }

  private async Task RunMutationAsync(Func<Task> action, Func<Task>? recover = null)
  {
    IsBusy = true;
    ErrorMessage = null;
    try { await action().ConfigureAwait(true); }
    catch (Exception ex) when (IsExpectedFailure(ex))
    {
      if (recover is not null)
      {
        try { await recover().ConfigureAwait(true); }
        catch (Exception recoveryError) when (IsExpectedFailure(recoveryError)) { }
      }
      ErrorMessage = ex.Message;
    }
    finally { IsBusy = false; }
  }

  private static bool IsExpectedFailure(Exception exception) =>
      exception is VouchaApiException or HttpRequestException or InvalidOperationException or TaskCanceledException;

  private static bool IsInbound(SupportMessage message) =>
      string.Equals(message.Direction, "inbound", StringComparison.Ordinal);

  private static bool IsUnsentDraft(SupportMessage message) =>
      message.DraftedAt is not null && message.SentAt is null;

  private bool HasAdministrator => !string.IsNullOrWhiteSpace(administratorId);
}

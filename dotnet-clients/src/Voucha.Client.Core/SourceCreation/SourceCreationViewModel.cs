using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.SourceCreation;

public sealed partial class SourceCreationViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly ISourceCreationService service;
  private readonly ISessionStore session;
  private readonly IDisposable? localeSubscription;
  private readonly IUiLocalization localization;
  private readonly Func<TimeSpan, CancellationToken, Task> delay;
  private CancellationTokenSource? cancellation;
  private int generation;
  private bool disposed;
  private bool busy;
  private string input = string.Empty;
  private string? importId;
  private Uri? origin;
  private RssFeedImportSummary? summary;
  private ImportResult? result;
  private UiText? error;
  private string? resolvedTopic;
  private bool validated;
  private bool submitted;

  public SourceCreationViewModel(ISourceCreationService service, ISessionStore session,
      IUiLocalization localization, Func<TimeSpan, CancellationToken, Task>? delay = null, IUiLocaleController? localeController = null)
  {
    ArgumentNullException.ThrowIfNull(session);
    this.service = service;
    this.session = session;
    this.localization = localization;
    this.delay = delay ?? Task.Delay;
    session.SessionChanged += OnSessionChanged;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public string Input { get => input; set => SetProperty(ref input, value); }
  public bool IsBusy => busy;
  public bool CanCreate => !disposed && session.Current.IsAuthenticated && !busy && !submitted;
  public bool CanRetry => !disposed && !busy && validated && importId is not null && !CanOpen && result?.IsTerminalFailure() != true;
  public string? Error => error is { } value ? localization.Resolve(value) : null;
  public string? Status => result?.IsTerminalFailure() == true
      ? localization.Resolve(UiText.Localized(UiMessageKey.NativeSwiftImportExportFailed,
          ("error", UiText.Localized(UiMessageKey.NativeDotnetCsharpActionFailed))))
      : result is { } row
      ? new ImportResultPresentation(row, localization).LocalizedDisplayStatus
      : importId is null ? null : localization.Localize(UiMessageKey.NativeSwiftImportExportPending);
  public string? EntityId => result?.EntityId;
  public string? TopicSlug => result?.TopicSlug ?? resolvedTopic;
  public bool CanOpen => summary?.IsTerminal() == true && result?.IsTerminalFailure() == false &&
      !string.IsNullOrWhiteSpace(EntityId) && !string.IsNullOrWhiteSpace(TopicSlug);

  public void Cancel()
  {
    generation++;
    cancellation?.Cancel();
    cancellation?.Dispose();
    cancellation = null;
    busy = false;
    Publish();
  }

  private void OnSessionChanged(object? sender, SessionChangedEventArgs args)
  {
    Cancel();
    importId = null;
    validated = false;
    submitted = false;
    summary = null;
    result = null;
    resolvedTopic = null;
    error = null;
    Publish();
  }

  public void OnUiLocaleChanged() => Publish();

  public void Dispose()
  {
    disposed = true;
    localeSubscription?.Dispose();
    session.SessionChanged -= OnSessionChanged;
    Cancel();
  }

  private void Publish()
  {
    foreach (var property in new[] { nameof(IsBusy), nameof(CanCreate), nameof(CanRetry), nameof(Error),
        nameof(Status), nameof(EntityId), nameof(TopicSlug), nameof(CanOpen) }) OnPropertyChanged(property);
  }
}

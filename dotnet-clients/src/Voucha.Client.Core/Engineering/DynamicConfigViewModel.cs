using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.FeatureFlags;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Engineering;

public sealed partial class DynamicConfigViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IDynamicConfigService service;
  private readonly FeatureFlagState featureFlags;
  private readonly IUiLocalization localization;
  private readonly IUiLocaleController? localeController;
  private readonly IDisposable? localeSubscription;
  private IReadOnlyList<DynamicConfigNamespaceSummary> namespaces = [];
  private IReadOnlyList<DynamicConfigNamespaceSummary> filteredNamespaces = [];
  private IReadOnlyList<DynamicConfigFieldViewModel> fields = [];
  private IReadOnlyList<DynamicConfigHistoryEntry> history = [];
  private DynamicConfigNamespace? selectedNamespace;
  private string searchText = string.Empty;
  private string? errorMessage;
  private UiText? localizedErrorText;
  private string? feedbackMessage;
  private UiText? localizedFeedbackText;
  private bool isLoading;
  private bool isSaving;
  private bool isSelecting;
  private int selectionVersion;
  private int loadInProgress;

  public const string FeatureFlagsNamespace = "feature-flags";

  public DynamicConfigViewModel(
      IDynamicConfigService service,
      FeatureFlagState featureFlags,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
    this.localization = localization ?? UiLocalization.English;
    this.localeController = localeController;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public IReadOnlyList<DynamicConfigNamespaceSummary> FilteredNamespaces { get => filteredNamespaces; private set => SetProperty(ref filteredNamespaces, value); }
  public IReadOnlyList<DynamicConfigNamespaceOption> FilteredNamespaceOptions =>
      FilteredNamespaces.Select(item => new DynamicConfigNamespaceOption(item)).ToArray();
  public IReadOnlyList<DynamicConfigFieldViewModel> Fields { get => fields; private set => SetProperty(ref fields, value); }
  public IReadOnlyList<DynamicConfigHistoryEntry> History { get => history; private set => SetProperty(ref history, value); }
  public DynamicConfigNamespace? SelectedNamespace { get => selectedNamespace; private set { if (SetProperty(ref selectedNamespace, value)) NotifySelection(); } }
  public string SearchText { get => searchText; set { if (SetProperty(ref searchText, value)) ApplySearch(); } }
  public string? ErrorMessage
  {
    get => localizedErrorText is { } text ? localization.Resolve(text) : errorMessage;
    private set
    {
      localizedErrorText = null;
      if (SetProperty(ref errorMessage, value)) OnPropertyChanged(nameof(HasError));
    }
  }
  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
  public string? FeedbackMessage =>
      localizedFeedbackText is { } text ? localization.Resolve(text) : feedbackMessage;
  public string? LocalizedFeedbackMessage => FeedbackMessage;
  public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
  public bool IsSaving { get => isSaving; private set { if (SetProperty(ref isSaving, value)) OnPropertyChanged(nameof(CanMutate)); } }
  public bool IsSelecting { get => isSelecting; private set { if (SetProperty(ref isSelecting, value)) OnPropertyChanged(nameof(CanMutate)); } }
  public bool CanMutate => SelectedNamespace?.CanUpdate == true && !IsSaving && !IsSelecting;
  public bool IsReadOnly => SelectedNamespace is { CanUpdate: false };

  public void OnUiLocaleChanged()
  {
    foreach (var field in Fields) field.OnUiLocaleChanged();
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(FeedbackMessage));
    OnPropertyChanged(nameof(Fields));
  }

  public void Dispose() => localeSubscription?.Dispose();

  private void SetLocalizedError(UiText? text)
  {
    localizedErrorText = text;
    errorMessage = null;
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(HasError));
  }

  private void SetLocalizedFeedback(UiText? text)
  {
    localizedFeedbackText = text;
    feedbackMessage = null;
    OnPropertyChanged(nameof(FeedbackMessage));
  }

  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (Interlocked.CompareExchange(ref loadInProgress, 1, 0) != 0) return;
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      namespaces = (await service.FetchNamespacesAsync(cancellationToken).ConfigureAwait(true)).Namespaces;
      ApplySearch();
      if (SelectedNamespace is null && FilteredNamespaces.Count > 0) await SelectAsync(FilteredNamespaces[0].Namespace, cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex) when (IsExpected(ex)) { ErrorMessage = ex.Message; }
    finally
    {
      IsLoading = false;
      Volatile.Write(ref loadInProgress, 0);
    }
  }

  public async Task SelectAsync(string namespaceName, CancellationToken cancellationToken = default)
  {
    var version = ++selectionVersion;
    IsSelecting = true;
    ErrorMessage = null;
    SetLocalizedFeedback(null);
    try
    {
      var detailTask = service.FetchNamespaceAsync(namespaceName, cancellationToken);
      var historyTask = service.FetchHistoryAsync(namespaceName, cancellationToken);
      var detail = await detailTask.ConfigureAwait(true);
      var historyResult = await historyTask.ConfigureAwait(true);
      if (version != selectionVersion) return;
      ApplyNamespace(detail.Namespace);
      History = historyResult.History;
    }
    catch (Exception ex) when (IsExpected(ex)) { if (version == selectionVersion) ErrorMessage = ex.Message; }
    finally { if (version == selectionVersion) IsSelecting = false; }
  }

  public async Task SaveBooleanAsync(DynamicConfigFieldViewModel field, bool value, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(field);
    if (!TryGetMutationTarget(field, out var target)) return;
    var previous = field.Field;
    field.SetBoolean(value);
    var succeeded = await SaveAsync(field, DynamicConfigValues.From(value), target, cancellationToken).ConfigureAwait(true);
    if (!succeeded && IsCurrent(target) && Fields.Contains(field)) field.Apply(previous);
  }

  public async Task SaveDraftAsync(DynamicConfigFieldViewModel field, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(field);
    if (TryGetMutationTarget(field, out var target) && field.TryValue(out var value))
    {
      await SaveAsync(field, value, target, cancellationToken).ConfigureAwait(true);
    }
  }
}

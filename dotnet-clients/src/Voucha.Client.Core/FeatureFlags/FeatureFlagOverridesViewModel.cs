using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.FeatureFlags;

public sealed record FeatureFlagOverrideItem(
    string Key,
    bool GlobalValue,
    bool EffectiveValue,
    bool? LocalOverride);

public sealed class FeatureFlagOverridesViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IFeatureFlagService service;
  private readonly FeatureFlagState state;
  private readonly ISessionStore sessionStore;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private IReadOnlyList<FeatureFlagOverrideItem> flags = [];
  private string? errorMessage;
  private UiText? localizedErrorText;
  private bool isLoading;
  private bool isSaving;

  public FeatureFlagOverridesViewModel(
      IFeatureFlagService service,
      FeatureFlagState state,
      ISessionStore sessionStore,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.state = state ?? throw new ArgumentNullException(nameof(state));
    this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public IReadOnlyList<FeatureFlagOverrideItem> Flags { get => flags; private set => SetProperty(ref flags, value); }
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
  public bool IsLoading { get => isLoading; private set { if (SetProperty(ref isLoading, value)) OnPropertyChanged(nameof(CanMutate)); } }
  public bool IsSaving { get => isSaving; private set { if (SetProperty(ref isSaving, value)) OnPropertyChanged(nameof(CanMutate)); } }
  public bool IsAuthorized => FeatureFlagOverridePolicy.CanManage(sessionStore.Current.Identity?.Roles);
  public bool CanMutate => IsAuthorized && state.IsRemoteHydrated && !IsLoading && !IsSaving;
  public bool HasLocalOverrides => state.LocalOverrides.Count > 0;

  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    Flags = [];
    ErrorMessage = null;
    OnPropertyChanged(nameof(IsAuthorized));
    if (!IsAuthorized) return;
    IsLoading = true;
    try
    {
      await state.InitializeAsync(cancellationToken).ConfigureAwait(true);
      var ticket = await state.BeginRemoteReadAsync(cancellationToken).ConfigureAwait(true);
      var response = await service.FetchAsync(cancellationToken).ConfigureAwait(true);
      await state.ApplyRemoteReadAsync(ticket, response.Flags, cancellationToken).ConfigureAwait(true);
      Rebuild();
    }
    catch (Exception ex) when (IsExpected(ex))
    {
      SetLocalizedError(UiText.Localized(
          UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsLoadFlagsFailedReason,
          ("error", UiText.ExternalContent(ex.Message))));
    }
    finally { IsLoading = false; }
  }

  public Task<bool> SetOverrideAsync(string key, bool value, CancellationToken cancellationToken = default) =>
      MutateAsync(() => state.SetOverrideAsync(key, value, cancellationToken));

  public Task<bool> RemoveOverrideAsync(string key, CancellationToken cancellationToken = default) =>
      MutateAsync(() => state.RemoveOverrideAsync(key, cancellationToken));

  public Task<bool> ClearOverridesAsync(CancellationToken cancellationToken = default) =>
      MutateAsync(() => state.ClearOverridesAsync(cancellationToken));

  private async Task<bool> MutateAsync(Func<Task> action)
  {
    if (!CanMutate) return false;
    IsSaving = true;
    ErrorMessage = null;
    try
    {
      await action().ConfigureAwait(true);
      Rebuild();
      return true;
    }
    catch (Exception ex) when (IsExpected(ex))
    {
      SetLocalizedError(UiText.Localized(
          UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsSaveOverrideFailedReason,
          ("error", UiText.ExternalContent(ex.Message))));
      return false;
    }
    finally { IsSaving = false; }
  }

  private void Rebuild()
  {
    Flags = state.RemoteFlags.OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new FeatureFlagOverrideItem(
            pair.Key,
            pair.Value,
            state.EffectiveFlags[pair.Key],
            state.LocalOverrides.TryGetValue(pair.Key, out var local) ? local : null))
        .ToArray();
    OnPropertyChanged(nameof(HasLocalOverrides));
  }

  private static bool IsExpected(Exception ex) =>
      ex is HttpRequestException or InvalidOperationException or System.Text.Json.JsonException or IOException;

  public void OnUiLocaleChanged() => OnPropertyChanged(nameof(ErrorMessage));

  public void Dispose() => localeSubscription?.Dispose();

  private void SetLocalizedError(UiText text)
  {
    localizedErrorText = text;
    errorMessage = null;
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(HasError));
  }
}

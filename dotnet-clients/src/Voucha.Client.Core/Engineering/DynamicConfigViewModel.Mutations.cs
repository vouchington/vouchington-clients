using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Engineering;

public sealed partial class DynamicConfigViewModel
{
  private async Task<bool> SaveAsync(
      DynamicConfigFieldViewModel field,
      DynamicConfigValue value,
      MutationTarget target,
      CancellationToken cancellationToken)
  {
    IsSaving = true;
    ErrorMessage = null;
    SetLocalizedFeedback(null);
    try
    {
      var response = await service.UpdateFieldAsync(target.Namespace, field.Name, value, cancellationToken).ConfigureAwait(true);
      if (response.Namespace.Namespace == FeatureFlagsNamespace)
      {
        await ApplyRemoteFeatureFlagsAsync(response.Namespace).ConfigureAwait(true);
      }
      if (!IsCurrent(target)) return true;
      ApplyNamespaceAfterSave(response.Namespace, field);
      SetLocalizedFeedback(UiText.Localized(response.Changed
          ? UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsUpdated
          : UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsNoChange));
      if (response.Changed)
      {
        try
        {
          var refreshedHistory = await service.FetchHistoryAsync(target.Namespace, cancellationToken).ConfigureAwait(true);
          if (IsCurrent(target)) History = refreshedHistory.History;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
          if (IsCurrent(target))
            SetLocalizedError(UiText.Localized(
                UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsUpdatedHistoryFailedReason,
                ("error", UiText.ExternalContent(ex.Message))));
        }
      }
      return true;
    }
    catch (Exception ex) when (IsExpected(ex))
    {
      if (IsCurrent(target))
        SetLocalizedError(UiText.Localized(
            UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsUpdateFailedReason,
            ("error", UiText.ExternalContent(ex.Message))));
      return false;
    }
    finally { IsSaving = false; }
  }

  private bool TryGetMutationTarget(DynamicConfigFieldViewModel field, out MutationTarget target)
  {
    if (CanMutate && Fields.Contains(field) && SelectedNamespace is { } selected)
    {
      target = new MutationTarget(selected.Namespace, selectionVersion);
      return true;
    }
    target = default;
    return false;
  }

  private bool IsCurrent(MutationTarget target) =>
      selectionVersion == target.SelectionVersion && SelectedNamespace?.Namespace == target.Namespace;

  private void ApplyNamespace(DynamicConfigNamespace value)
  {
    SelectedNamespace = value;
    Fields = value.Fields.Select(field => new DynamicConfigFieldViewModel(
        field,
        localization)).ToArray();
  }

  private void ApplyNamespaceAfterSave(DynamicConfigNamespace value, DynamicConfigFieldViewModel savedField)
  {
    var currentFields = new Dictionary<string, Queue<DynamicConfigFieldViewModel>>(StringComparer.Ordinal);
    foreach (var field in Fields)
    {
      if (!currentFields.TryGetValue(field.Name, out var matches))
      {
        matches = new Queue<DynamicConfigFieldViewModel>();
        currentFields.Add(field.Name, matches);
      }
      matches.Enqueue(field);
    }
    SelectedNamespace = value;
    Fields = value.Fields.Select(field =>
    {
      if (!currentFields.TryGetValue(field.Name, out var matches) || matches.Count == 0)
      {
        return new DynamicConfigFieldViewModel(
            field,
            localization);
      }
      var current = matches.Dequeue();
      if (ReferenceEquals(current, savedField)) current.Apply(field);
      else current.ApplyPreservingDraft(field);
      return current;
    }).ToArray();
  }

  private Task ApplyRemoteFeatureFlagsAsync(DynamicConfigNamespace value)
  {
    return featureFlags.ApplyAuthoritativeRemoteAsync(
        value.Config
            .Where(pair => pair.Value is DynamicConfigBooleanValue)
            .ToDictionary(pair => pair.Key, pair => ((DynamicConfigBooleanValue)pair.Value).Value, StringComparer.Ordinal));
  }

  private void ApplySearch()
  {
    FilteredNamespaces = namespaces.Where(item =>
        item.Label.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
        item.Namespace.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
        item.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToArray();
    OnPropertyChanged(nameof(FilteredNamespaceOptions));
  }

  private void NotifySelection()
  {
    OnPropertyChanged(nameof(CanMutate));
    OnPropertyChanged(nameof(IsReadOnly));
  }

  private static bool IsExpected(Exception ex) =>
      ex is VouchaApiException or HttpRequestException or InvalidOperationException or System.Text.Json.JsonException or IOException;

  private readonly record struct MutationTarget(string Namespace, int SelectionVersion);
}

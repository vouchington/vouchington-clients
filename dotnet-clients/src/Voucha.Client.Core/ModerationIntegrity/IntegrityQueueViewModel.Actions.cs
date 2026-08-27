namespace Voucha.Client.Core.ModerationIntegrity;

using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

public abstract partial class IntegrityQueueViewModel<TFlag, TRow>
{
  public bool IsActionInFlight(string flagId) => actionIds.Contains(flagId);

  public string? ActionError(string flagId) =>
      actionErrors.TryGetValue(flagId, out var message) ? Localization.Resolve(message) : null;

  public bool NeedsReconciliation(string flagId) => reconciliationIds.Contains(flagId);

  protected TFlag? Find(string flagId) =>
      Flags().FirstOrDefault(flag => string.Equals(Id(flag), flagId, StringComparison.Ordinal));

  protected bool CanBeginAction(string flagId, bool capability) =>
      IsAuthorized && capability &&
      Find(flagId) is { } flag &&
      !IsResolved(flag) &&
      !IsActionInFlight(flagId) &&
      !NeedsReconciliation(flagId);

  protected bool BeginAction(string flagId, bool capability)
  {
    if (!CanBeginAction(flagId, capability)) return false;
    actionIds.Add(flagId);
    actionErrors.Remove(flagId);
    ActionsChanged();
    return true;
  }

  protected void Replace(TFlag flag)
  {
    var nextId = Id(flag);
    if (Status == IntegrityFlagStatus.Pending && IsResolved(flag))
    {
      Items = Flags()
          .Where(item => !string.Equals(Id(item), nextId, StringComparison.Ordinal))
          .Select(Row)
          .ToArray();
      return;
    }
    Items = Flags()
        .Select(item => string.Equals(Id(item), nextId, StringComparison.Ordinal) ? Row(flag) : Row(item))
        .ToArray();
  }

  protected void CompleteAction(string flagId)
  {
    actionIds.Remove(flagId);
    actionErrors.Remove(flagId);
    ActionsChanged();
  }

  protected void FailAction(string flagId, Exception exception)
  {
    ArgumentNullException.ThrowIfNull(exception);
    actionIds.Remove(flagId);
    actionErrors[flagId] = UiText.ExternalContent(exception.Message);
    ActionsChanged();
  }

  protected async Task ReconcileAmbiguousAsync(
      string flagId,
      Func<CancellationToken, Task<TFlag>> fetchExact)
  {
    ArgumentNullException.ThrowIfNull(fetchExact);
    reconciliationIds.Add(flagId);
    actionErrors[flagId] = UiText.Localized(UiMessageKey.NativeSwiftIntegrityResultUncertain);
    ActionsChanged();
    try
    {
      var authoritative = await fetchExact(CancellationToken.None).ConfigureAwait(true);
      Replace(authoritative);
      reconciliationIds.Remove(flagId);
      CompleteAction(flagId);
    }
    catch (Exception exception) when (IntegrityMutationFailure.IsExpected(exception) ||
        exception is OperationCanceledException)
    {
      actionIds.Remove(flagId);
      actionErrors[flagId] = UiText.Localized(
          UiMessageKey.NativeSwiftIntegrityReconciliationFailed);
      ActionsChanged();
    }
  }

  protected void RequireReconciliation(string flagId)
  {
    actionIds.Remove(flagId);
    reconciliationIds.Add(flagId);
    actionErrors[flagId] = UiText.Localized(UiMessageKey.NativeSwiftIntegrityResultUncertain);
    ActionsChanged();
  }

  protected bool BeginReconciliationAction(string flagId)
  {
    if (!NeedsReconciliation(flagId) || !actionIds.Add(flagId)) return false;
    ActionsChanged();
    return true;
  }

  protected void CompletePenaltyReconciliation(string flagId)
  {
    actionIds.Remove(flagId);
    reconciliationIds.Remove(flagId);
    actionErrors.Remove(flagId);
    ActionsChanged();
  }

  private void ActionsChanged()
  {
    OnPropertyChanged(nameof(Items));
    OnPropertyChanged(nameof(HasError));
  }
}

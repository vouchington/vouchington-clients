using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Topics;

/// <summary>Retains one accepted merge and retries only its source-scoped status read.</summary>
public sealed class TopicMergeViewModel(ITopicsService service) : ObservableObject
{
  private string? accountId;
  private long generation;
  public TopicMergeDetails? Operation { get; private set; }
  public UiText? Message { get; private set; }
  public bool IsBusy { get; private set; }
  public bool CanSubmit => !IsBusy && Operation is null;
  public bool CanCheck => !IsBusy && Operation is not null && TopicMergeProgress.CanCheckStatus(Operation);

  public void SetAccount(string? value)
  {
    if (accountId == value) return;
    accountId = value;
    generation++;
    Operation = null;
    Message = null;
    IsBusy = false;
    OnPropertyChanged(string.Empty);
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Merge failures are presented inline.")]
  public async Task SubmitAsync(string source, string destination, CancellationToken cancellationToken = default)
  {
    if (!CanSubmit) return;
    var request = ++generation;
    IsBusy = true;
    OnPropertyChanged(string.Empty);
    try
    {
      var response = await service.MergeTopicAliasesAsync(source, destination, cancellationToken).ConfigureAwait(true);
      if (request != generation) return;
      Apply(response.TopicMerge);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (request == generation) Message = UiText.Verbatim(ex.Message);
    }
    finally
    {
      if (request == generation)
      {
        IsBusy = false;
        OnPropertyChanged(string.Empty);
      }
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Status failures remain retryable inline.")]
  public async Task CheckAsync(CancellationToken cancellationToken = default)
  {
    if (!CanCheck || Operation is not { } operation) return;
    var request = generation;
    IsBusy = true;
    OnPropertyChanged(string.Empty);
    try
    {
      var response = await service.FetchTopicMergeStatusAsync(operation.SourceTopicId, operation.Id, cancellationToken).ConfigureAwait(true);
      if (request != generation || Operation?.Id != operation.Id ||
          response.TopicMerge.Id != operation.Id || response.TopicMerge.SourceTopicId != operation.SourceTopicId) return;
      Apply(response.TopicMerge);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (request == generation) Message = UiText.Localized(UiMessageKey.SettingsTopicsMergeStatusFailed);
    }
    finally
    {
      if (request == generation)
      {
        IsBusy = false;
        OnPropertyChanged(string.Empty);
      }
    }
  }

  private void Apply(TopicMergeDetails operation)
  {
    Operation = operation;
    Message = UiText.Localized(TopicMergeProgress.MessageFor(operation));
  }
}

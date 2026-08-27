using System.IO;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Engineering;

public sealed partial class EngineeringQueuesViewModel : ObservableObject
{
  private readonly IEngineeringService service;
  private QueueStatsSummary? stats;
  private IReadOnlyList<QueueStats> queues = [];
  private IReadOnlyList<ScheduledJob> scheduledJobs = [];
  private IReadOnlyList<Backfill> backfills = [];
  private bool isLoading;
  private bool isMutating;
  private string? errorMessage;

  public EngineeringQueuesViewModel(IEngineeringService service) =>
      this.service = service ?? throw new ArgumentNullException(nameof(service));

  public QueueStatsSummary? Stats { get => stats; private set => SetProperty(ref stats, value); }
  public IReadOnlyList<QueueStats> Queues { get => queues; private set => SetProperty(ref queues, value); }
  public IReadOnlyList<ScheduledJob> ScheduledJobs { get => scheduledJobs; private set => SetProperty(ref scheduledJobs, value); }
  public IReadOnlyList<Backfill> Backfills { get => backfills; private set => SetProperty(ref backfills, value); }

  public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      if (SetProperty(ref errorMessage, value))
      {
        OnPropertyChanged(nameof(HasError));
      }
    }
  }
  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading) return;
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      ErrorMessage = null;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException or JsonException or IOException)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsLoading = false;
    }
  }

  public Task PauseQueueAsync(string name, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.PauseQueueAsync(name, cancellationToken), cancellationToken);

  public Task ResumeQueueAsync(string name, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.ResumeQueueAsync(name, cancellationToken), cancellationToken);

  public Task TriggerScheduledJobAsync(string id, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.TriggerScheduledJobAsync(id, cancellationToken), cancellationToken);

  public Task TriggerBackfillAsync(string id, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.TriggerBackfillAsync(id, cancellationToken), cancellationToken);

  private async Task RunAndReloadAsync(Func<Task> action, CancellationToken cancellationToken)
  {
    if (IsLoading || isMutating) return;
    isMutating = true;
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      await action().ConfigureAwait(true);
      await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      ErrorMessage = null;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException or JsonException or IOException)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      isMutating = false;
      IsLoading = false;
    }
  }

  private async Task ReloadAsync(CancellationToken cancellationToken)
  {
    var statsTask = service.FetchQueueStatsAsync(cancellationToken);
    var queuesTask = service.FetchQueuesAsync(cancellationToken);
    var scheduledTask = service.FetchScheduledJobsAsync(cancellationToken);
    var backfillsTask = service.FetchBackfillsAsync(cancellationToken);
    await Task.WhenAll(statsTask, queuesTask, scheduledTask, backfillsTask).ConfigureAwait(true);

    Stats = (await statsTask.ConfigureAwait(true)).Stats;
    Queues = (await queuesTask.ConfigureAwait(true)).Queues;
    ScheduledJobs = (await scheduledTask.ConfigureAwait(true)).Jobs;
    Backfills = (await backfillsTask.ConfigureAwait(true)).Backfills;
  }
}

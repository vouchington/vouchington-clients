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
  private IReadOnlyList<QueueDomain> domains = [];
  public IReadOnlyList<QueueDomain> Domains
  {
    get => domains;
    private set
    {
      if (SetProperty(ref domains, value))
      {
        OnPropertyChanged(nameof(HasDomains));
        OnPropertyChanged(nameof(DomainRows));
      }
    }
  }
  public bool HasDomains => Domains.Count > 0;
  public IReadOnlyList<QueueDomainRow> DomainRows => Domains.Select(domain => new QueueDomainRow(domain)).ToArray();
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

  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      EngineeringRequest.RunAsync(
          () => IsLoading,
          () => IsLoading = true,
          message => ErrorMessage = message,
          () => IsLoading = false,
          () => ReloadAsync(cancellationToken));

  public Task PauseQueueAsync(string name, CancellationToken cancellationToken = default) =>
      name == "background" ? Task.CompletedTask : RunAndReloadAsync(() => service.PauseQueueAsync(name, cancellationToken), cancellationToken);

  public Task ResumeQueueAsync(string name, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.ResumeQueueAsync(name, cancellationToken), cancellationToken);

  public Task TriggerScheduledJobAsync(string id, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.TriggerScheduledJobAsync(id, cancellationToken), cancellationToken);

  public Task TriggerBackfillAsync(string id, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.TriggerBackfillAsync(id, cancellationToken), cancellationToken);

  private Task RunAndReloadAsync(Func<Task> action, CancellationToken cancellationToken) =>
      EngineeringRequest.RunAsync(
          () => IsLoading || isMutating,
          () =>
          {
            isMutating = true;
            IsLoading = true;
          },
          message => ErrorMessage = message,
          () =>
          {
            isMutating = false;
            IsLoading = false;
          },
          async () =>
          {
            await action().ConfigureAwait(true);
            await ReloadAsync(cancellationToken).ConfigureAwait(true);
          });

  private async Task ReloadAsync(CancellationToken cancellationToken)
  {
    var statsTask = service.FetchQueueStatsAsync(cancellationToken);
    var queuesTask = service.FetchQueuesAsync(cancellationToken);
    var scheduledTask = service.FetchScheduledJobsAsync(cancellationToken);
    var backfillsTask = service.FetchBackfillsAsync(cancellationToken);
    await Task.WhenAll(statsTask, queuesTask, scheduledTask, backfillsTask).ConfigureAwait(true);

    var loadedQueues = (await queuesTask.ConfigureAwait(true)).Queues;
    IReadOnlyList<QueueDomain> loadedDomains = loadedQueues.Any(queue => queue.Name == "background")
        ? (await service.FetchQueueDomainsAsync("background", cancellationToken).ConfigureAwait(true)).Domains
        : [];
    Stats = (await statsTask.ConfigureAwait(true)).Stats;
    Queues = loadedQueues;
    Domains = loadedDomains;
    ScheduledJobs = (await scheduledTask.ConfigureAwait(true)).Jobs;
    Backfills = (await backfillsTask.ConfigureAwait(true)).Backfills;
  }
}

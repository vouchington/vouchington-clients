using System.IO;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Engineering;

public sealed partial class EngineeringPostgreSqlViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private const string ActiveStatus = "active";
  private const string CompletedStatus = "completed";
  private const string FailedStatus = "failed";
  private readonly IEngineeringService service;
  private PsqlMigrationsResponse? migrations;
  private PartitionStatusResponse? partitions;
  private ArticleSyncJobStatusResponse? articleSyncStatus;
  private string articleSyncJobId = string.Empty;
  private bool isLoading;
  private bool isMutating;
  private string? errorMessage;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;

  public EngineeringPostgreSqlViewModel(
      IEngineeringService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public PsqlMigrationsResponse? Migrations { get => migrations; private set => SetProperty(ref migrations, value); }
  public PartitionStatusResponse? Partitions { get => partitions; private set => SetProperty(ref partitions, value); }

  public string ArticleSyncJobId
  {
    get => articleSyncJobId;
    set
    {
      if (SetProperty(ref articleSyncJobId, value ?? string.Empty))
      {
        ErrorMessage = null;
        ArticleSyncStatus = null;
        OnPropertyChanged(nameof(ArticleSyncStatusMessage));
      }
    }
  }

  public ArticleSyncJobStatusResponse? ArticleSyncStatus
  {
    get => articleSyncStatus;
    private set
    {
      if (SetProperty(ref articleSyncStatus, value))
      {
        OnPropertyChanged(nameof(ArticleSyncStatusMessage));
      }
    }
  }

  public string? ArticleSyncStatusMessage => FormatArticleSyncStatus();

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

  public Task RunMigrationsAsync(CancellationToken cancellationToken = default) =>
      RunJobAsync("runMigrations", cancellationToken);

  public Task RunViewsAsync(CancellationToken cancellationToken = default) =>
      RunJobAsync("runViews", cancellationToken);

  public Task RunConfigDrivenAsync(CancellationToken cancellationToken = default) =>
      RunJobAsync("runConfigDriven", cancellationToken);

  public Task CreatePartitionsAsync(CancellationToken cancellationToken = default) =>
      RunJobAsync("createPartitions", cancellationToken);

  public Task CleanupPartitionsAsync(CancellationToken cancellationToken = default) =>
      RunJobAsync("cleanupPartitions", cancellationToken);

  public async Task TriggerArticleSyncAsync(CancellationToken cancellationToken = default)
  {
    await RunAndRefreshAsync(
        async () =>
        {
          var response = await service.TriggerArticleSyncAsync(cancellationToken).ConfigureAwait(true);
          ArticleSyncJobId = response.JobId;
        },
        cancellationToken).ConfigureAwait(true);
    if (!string.IsNullOrWhiteSpace(ArticleSyncJobId))
    {
      await RefreshArticleSyncStatusAsync(cancellationToken).ConfigureAwait(true);
    }
  }

  public async Task RefreshArticleSyncStatusAsync(CancellationToken cancellationToken = default)
  {
    var jobId = ArticleSyncJobId;
    if (string.IsNullOrWhiteSpace(jobId)) return;
    try
    {
      var status = await service.FetchArticleSyncStatusAsync(jobId, cancellationToken).ConfigureAwait(true);
      if (string.Equals(jobId, ArticleSyncJobId, StringComparison.Ordinal))
      {
        ArticleSyncStatus = status;
      }
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException or JsonException or IOException)
    {
      ErrorMessage = ex.Message;
    }
  }

  private Task RunJobAsync(string type, CancellationToken cancellationToken) =>
      RunAndRefreshAsync(() => service.EnqueuePsqlJobAsync(type, cancellationToken), cancellationToken);

  private async Task RunAndRefreshAsync(Func<Task> action, CancellationToken cancellationToken)
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
    var migrationsTask = service.FetchMigrationsAsync(cancellationToken);
    var partitionsTask = service.FetchPartitionsAsync(cancellationToken);
    await Task.WhenAll(migrationsTask, partitionsTask).ConfigureAwait(true);
    Migrations = await migrationsTask.ConfigureAwait(true);
    Partitions = await partitionsTask.ConfigureAwait(true);
    if (!string.IsNullOrWhiteSpace(ArticleSyncJobId))
    {
      ArticleSyncStatus = await service.FetchArticleSyncStatusAsync(ArticleSyncJobId, cancellationToken).ConfigureAwait(true);
    }
  }

}

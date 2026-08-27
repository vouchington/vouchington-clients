using System.IO;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Engineering;

public sealed partial class EngineeringValkeyViewModel : ObservableObject
{
  private readonly IEngineeringService service;
  private IReadOnlyList<CacheGroup> cacheGroups = [];
  private bool isLoading;
  private string? errorMessage;
  private string? flushingConcern;

  public EngineeringValkeyViewModel(IEngineeringService service) =>
      this.service = service ?? throw new ArgumentNullException(nameof(service));

  public IReadOnlyList<CacheGroup> CacheGroups { get => cacheGroups; private set => SetProperty(ref cacheGroups, value); }
  public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
  public string? FlushingConcern
  {
    get => flushingConcern;
    private set
    {
      if (SetProperty(ref flushingConcern, value))
      {
        OnPropertyChanged(nameof(IsFlushing));
      }
    }
  }
  public bool IsFlushing => !string.IsNullOrWhiteSpace(FlushingConcern);

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Performance",
      "CA1822:Member can be marked as static",
      Justification = "Instance properties are required for compiled XAML bindings.")]
  public IReadOnlyList<FlushConcernOption> FlushConcerns => FlushConcernOption.All;
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
      CacheGroups = (await service.FetchCacheGroupsAsync(cancellationToken).ConfigureAwait(true)).Groups;
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

  public Task RebuildBloomFilterAsync(string filter, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.RebuildBloomFilterAsync(filter, cancellationToken), cancellationToken);

  public Task ClearCacheAsync(string group, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.ClearCacheAsync(group, cancellationToken), cancellationToken);

  public Task ClearAllAsync(CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.ClearCacheAsync("all", cancellationToken), cancellationToken);

  public Task FlushValkeyAsync(string concern, bool force = false, CancellationToken cancellationToken = default) =>
      RunFlushAsync(concern, () => service.FlushValkeyAsync(concern, force, cancellationToken), cancellationToken);

  private async Task RunFlushAsync(string concern, Func<Task> action, CancellationToken cancellationToken)
  {
    if (IsLoading) return;
    IsLoading = true;
    FlushingConcern = concern;
    ErrorMessage = null;
    try
    {
      await action().ConfigureAwait(true);
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
      FlushingConcern = null;
    }
  }

  private async Task RunAndReloadAsync(Func<Task> action, CancellationToken cancellationToken)
  {
    if (IsLoading) return;
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      await action().ConfigureAwait(true);
      CacheGroups = (await service.FetchCacheGroupsAsync(cancellationToken).ConfigureAwait(true)).Groups;
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
}

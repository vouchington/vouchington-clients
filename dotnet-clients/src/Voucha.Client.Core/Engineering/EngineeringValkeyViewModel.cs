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

  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      EngineeringRequest.RunAsync(
          () => IsLoading,
          () => IsLoading = true,
          message => ErrorMessage = message,
          () => IsLoading = false,
          async () =>
          {
            CacheGroups = (await service.FetchCacheGroupsAsync(cancellationToken).ConfigureAwait(true)).Groups;
          });

  public Task RebuildBloomFilterAsync(string filter, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.RebuildBloomFilterAsync(filter, cancellationToken), cancellationToken);

  public Task ClearCacheAsync(string group, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.ClearCacheAsync(group, cancellationToken), cancellationToken);

  public Task ClearAllAsync(CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.ClearCacheAsync("all", cancellationToken), cancellationToken);

  public Task FlushValkeyAsync(string concern, bool force = false, CancellationToken cancellationToken = default) =>
      RunFlushAsync(concern, () => service.FlushValkeyAsync(concern, force, cancellationToken), cancellationToken);

  private Task RunFlushAsync(string concern, Func<Task> action, CancellationToken cancellationToken) =>
      EngineeringRequest.RunAsync(
          () => IsLoading,
          () =>
          {
            IsLoading = true;
            FlushingConcern = concern;
          },
          message => ErrorMessage = message,
          () =>
          {
            IsLoading = false;
            FlushingConcern = null;
          },
          action);

  private Task RunAndReloadAsync(Func<Task> action, CancellationToken cancellationToken) =>
      EngineeringRequest.RunAsync(
          () => IsLoading,
          () => IsLoading = true,
          message => ErrorMessage = message,
          () => IsLoading = false,
          async () =>
          {
            await action().ConfigureAwait(true);
            CacheGroups = (await service.FetchCacheGroupsAsync(cancellationToken).ConfigureAwait(true)).Groups;
          });
}

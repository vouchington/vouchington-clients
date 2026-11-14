using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.ModerationIntegrity;

public interface IIntegrityFlagQueueModel
{
  IReadOnlyList<IntegrityFlagStatus> AvailableStatuses { get; }

  IntegrityFlagStatus Status { get; }

  string? ErrorMessage { get; }

  LoadState State { get; }

  bool IsLoading { get; }

  bool IsEmpty { get; }

  bool HasMore { get; }

  bool CanLoadMore { get; }

  Task LoadAsync(CancellationToken cancellationToken);

  Task ReloadAsync(CancellationToken cancellationToken);

  Task LoadMoreAsync(CancellationToken cancellationToken);

  Task SelectStatusAsync(IntegrityFlagStatus nextStatus, CancellationToken cancellationToken);
}

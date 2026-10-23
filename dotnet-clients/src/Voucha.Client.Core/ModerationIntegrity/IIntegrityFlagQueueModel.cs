using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ModerationIntegrity;

public interface IIntegrityFlagQueueModel
{
  IReadOnlyList<IntegrityFlagStatus> AvailableStatuses { get; }

  IntegrityFlagStatus Status { get; }

  Task LoadAsync(CancellationToken cancellationToken);

  Task ReloadAsync(CancellationToken cancellationToken);

  Task LoadMoreAsync(CancellationToken cancellationToken);

  Task SelectStatusAsync(IntegrityFlagStatus nextStatus, CancellationToken cancellationToken);
}

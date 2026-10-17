namespace Voucha.Client.Core.Engineering;

public sealed partial class EngineeringQueuesViewModel
{
  public Task PauseDomainAsync(string domain, string? reason = null, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.PauseQueueDomainAsync("background", domain, reason, cancellationToken), cancellationToken);

  public Task ResumeDomainAsync(string domain, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.ResumeQueueDomainAsync("background", domain, cancellationToken), cancellationToken);

  public Task RetryDomainAsync(string domain, CancellationToken cancellationToken = default) =>
      RunAndReloadAsync(() => service.RetryQueueDomainAsync("background", domain, cancellationToken), cancellationToken);
}

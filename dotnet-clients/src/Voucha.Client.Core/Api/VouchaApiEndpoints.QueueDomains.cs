namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest FetchQueueDomains(string name) =>
      Get($"/api/v1/mq/queues/{Path(name)}/domains");

  public static ApiRequest PauseQueueDomain(string name, string domain, string? reason = null) =>
      new(HttpMethod.Post, $"{QueueDomainPath(name, domain)}/pause")
      { Body = reason is null ? null : new PauseQueueDomainBody(reason) };

  public static ApiRequest ResumeQueueDomain(string name, string domain) =>
      new(HttpMethod.Post, $"{QueueDomainPath(name, domain)}/resume");

  public static ApiRequest RetryQueueDomain(string name, string domain) =>
      new(HttpMethod.Post, $"{QueueDomainPath(name, domain)}/retry-failed");

  private static string QueueDomainPath(string name, string domain) =>
      $"/api/v1/mq/queues/{Path(name)}/domains/{Path(domain)}";
}

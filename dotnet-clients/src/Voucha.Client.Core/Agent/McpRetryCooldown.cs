namespace Voucha.Client.Core.Agent;

/// <summary>A connection-local monotonic deadline; a refusal never schedules another request.</summary>
internal sealed class McpRetryCooldown(TimeProvider timeProvider)
{
  private readonly object gate = new();
  private long started;
  private TimeSpan delay;

  public McpRateLimitException FromResponse(HttpResponseMessage response)
  {
    var retry = response.Headers.RetryAfter;
    var candidate = retry?.Delta ?? (retry?.Date - timeProvider.GetUtcNow());
    return Limited(candidate > TimeSpan.Zero ? candidate : null);
  }

  public McpRateLimitException Limited(TimeSpan? retryAfter)
  {
    if (retryAfter is { } candidate && candidate > TimeSpan.Zero)
    {
      lock (gate)
      {
        if (candidate > Remaining())
        {
          started = timeProvider.GetTimestamp();
          delay = candidate;
        }
      }
    }
    return new McpRateLimitException(retryAfter);
  }

  public void Check()
  {
    lock (gate)
    {
      var remaining = Remaining();
      if (remaining > TimeSpan.Zero) throw new McpRateLimitException(remaining);
      delay = TimeSpan.Zero;
    }
  }

  private TimeSpan Remaining() => delay == TimeSpan.Zero
      ? TimeSpan.Zero : delay - timeProvider.GetElapsedTime(started);
}

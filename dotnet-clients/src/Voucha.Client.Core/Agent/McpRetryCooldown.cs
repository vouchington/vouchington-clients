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
    lock (gate)
    {
      var remaining = Remaining();
      if (retryAfter is { } candidate && candidate > TimeSpan.Zero && candidate > remaining)
      {
        started = timeProvider.GetTimestamp();
        delay = candidate;
        remaining = candidate;
      }
      return new McpRateLimitException(remaining > TimeSpan.Zero ? remaining : null);
    }
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

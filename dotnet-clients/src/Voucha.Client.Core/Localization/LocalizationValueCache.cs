namespace Voucha.Client.Core.Localization;

public sealed class LocalizationValueCache(int maxBytes = LocalizationValueCache.DefaultMaxBytes)
{
  public const int DefaultMaxBytes = 524_288;

  private readonly object gate = new();
  private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
  private readonly List<string> order = [];

  public string? Value(string key, string locale)
  {
    lock (gate)
    {
      if (!entries.TryGetValue(locale, out var entry)) return null;
      Touch(locale);
      return entry.Values.GetValueOrDefault(key);
    }
  }

  public string? Etag(string locale)
  {
    lock (gate)
    {
      return entries.TryGetValue(locale, out var entry) ? $"\"{entry.Revision}\"" : null;
    }
  }

  public bool IsExpired(string locale, DateTimeOffset now)
  {
    lock (gate)
    {
      return !entries.TryGetValue(locale, out var entry) || now >= entry.ExpiresAt;
    }
  }

  public void Apply(
      string locale,
      string revision,
      int ttlSeconds,
      IReadOnlyDictionary<string, string> values,
      DateTimeOffset now)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(locale);
    ArgumentException.ThrowIfNullOrWhiteSpace(revision);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ttlSeconds);
    ArgumentNullException.ThrowIfNull(values);
    lock (gate)
    {
      var replacement = new Dictionary<string, string>(values, StringComparer.Ordinal);
      entries[locale] = new Entry(
          revision,
          ttlSeconds,
          now.AddSeconds(ttlSeconds),
          replacement,
          replacement.Sum(static pair => EncodingByteCount(pair.Key) + EncodingByteCount(pair.Value)));
      Touch(locale);
      Evict();
    }
  }

  public void RememberNotModified(string locale, DateTimeOffset now)
  {
    lock (gate)
    {
      if (!entries.TryGetValue(locale, out var entry)) return;
      entries[locale] = entry with { ExpiresAt = now.AddSeconds(entry.TtlSeconds) };
      Touch(locale);
    }
  }

  public void Reset()
  {
    lock (gate)
    {
      entries.Clear();
      order.Clear();
    }
  }

  private void Touch(string locale)
  {
    order.Remove(locale);
    order.Add(locale);
  }

  private void Evict()
  {
    var total = entries.Values.Sum(static entry => entry.ByteCount);
    while (total > maxBytes && order.Count > 0)
    {
      var oldest = order[0];
      order.RemoveAt(0);
      if (entries.Remove(oldest, out var removed)) total -= removed.ByteCount;
    }
  }

  private static int EncodingByteCount(string value) => System.Text.Encoding.UTF8.GetByteCount(value);

  private readonly record struct Entry(
      string Revision,
      int TtlSeconds,
      DateTimeOffset ExpiresAt,
      Dictionary<string, string> Values,
      int ByteCount);
}

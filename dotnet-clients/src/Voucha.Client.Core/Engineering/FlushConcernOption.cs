namespace Voucha.Client.Core.Engineering;

public sealed record FlushConcernOption(string Concern, string Label, string Description, bool RequiresForce)
{
  public static IReadOnlyList<FlushConcernOption> All { get; } =
  [
    new("caches", "Caches", "Clears every entity cache group (users, topics, posts, rss, urls, elections).", false),
    new("recently-viewed", "Recently viewed", "Clears recently-viewed history for all entity types.", false),
    new("blooms", "Bloom filters", "Clears bloom-filter dedup state (URL/email blocklists, embeddings, entity cache, API keys).", false),
    new("rate-limiter", "Rate limiter", "Resets all rate-limiter throttling state.", false),
    new("dynamic-config", "Dynamic config", "Clears cached dynamic-config values; the next read re-fetches from Postgres.", false),
    new("sessions", "Sessions", "Force-logs out every user and invalidates in-flight passkey, MFA, and OAuth challenges.", true),
    new("queues", "Queues", "Obliterates every registered queue.", false),
  ];
}

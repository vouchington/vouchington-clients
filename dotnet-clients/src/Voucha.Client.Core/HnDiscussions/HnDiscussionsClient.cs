using System.Text.Json.Serialization;

namespace Voucha.Client.Core.HnDiscussions;

#pragma warning disable CA1054

public sealed record HnDiscussionThread(
    string ObjectId,
    string Title,
    int Score,
    int CommentCount,
    Uri ItemUrl);

public sealed class HnDiscussionsClient
{
  public const string SearchOrigin = "https://hn.algolia.com";
  private static readonly Uri SearchEndpoint = new("https://hn.algolia.com/api/v1/search");
  private readonly HttpClient httpClient;

  public HnDiscussionsClient(HttpClient httpClient) =>
      this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

  public static Uri BuildSearchUrl(string pageUrl)
  {
    var builder = new UriBuilder(SearchEndpoint)
    {
      Query = string.Join(
          '&',
          $"query={Uri.EscapeDataString(pageUrl)}",
          "restrictSearchableAttributes=url",
          "tags=story",
          "hitsPerPage=5"),
    };
    return builder.Uri;
  }

  public static IReadOnlyList<HnDiscussionThread> MapHits(string json, string sourceUrl)
  {
    var expected = HnDiscussionUrlCollector.Normalize(sourceUrl);
    if (expected is null) return [];
    var payload = System.Text.Json.JsonSerializer.Deserialize<AlgoliaSearchResponse>(json);
    if (payload?.Hits is null) return [];
    return payload.Hits.Select(hit => hit.ToThread(expected)).OfType<HnDiscussionThread>().ToList();
  }

  public async Task<IReadOnlyList<HnDiscussionThread>> SearchAsync(
      IEnumerable<string?> urls,
      CancellationToken cancellationToken = default)
  {
    var seen = new HashSet<string>(StringComparer.Ordinal);
    var threads = new List<HnDiscussionThread>();
    foreach (var url in HnDiscussionUrlCollector.Collect(urls))
    {
      foreach (var thread in await SearchAsync(url, cancellationToken).ConfigureAwait(false))
      {
        if (!seen.Add(thread.ObjectId)) continue;
        threads.Add(thread);
      }
    }
    return threads;
  }

  public async Task<IReadOnlyList<HnDiscussionThread>> SearchAsync(
      string pageUrl,
      CancellationToken cancellationToken = default)
  {
    try
    {
      using var response = await httpClient.GetAsync(BuildSearchUrl(pageUrl), cancellationToken)
          .ConfigureAwait(false);
      if (!response.IsSuccessStatusCode) return [];
      var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      return MapHits(json, pageUrl);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
    {
      return [];
    }
  }

  private sealed class AlgoliaSearchResponse
  {
    [JsonPropertyName("hits")]
    public AlgoliaHit[]? Hits { get; set; }
  }

  private sealed class AlgoliaHit
  {
    [JsonPropertyName("objectID")]
    public string? ObjectId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("points")]
    public int? Points { get; set; }

    [JsonPropertyName("num_comments")]
    public int? NumComments { get; set; }

    public HnDiscussionThread? ToThread(string expectedNormalizedUrl)
    {
      if (string.IsNullOrEmpty(ObjectId) || string.IsNullOrWhiteSpace(Title) || Url is null) return null;
      if (HnDiscussionUrlCollector.Normalize(Url) != expectedNormalizedUrl) return null;
      return new HnDiscussionThread(
          ObjectId,
          Title.Trim(),
          Points ?? 0,
          NumComments ?? 0,
          new Uri($"https://news.ycombinator.com/item?id={Uri.EscapeDataString(ObjectId)}"));
    }
  }
}

#pragma warning restore CA1054

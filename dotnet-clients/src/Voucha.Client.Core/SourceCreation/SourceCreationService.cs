using Voucha.Client.Core.Api;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.SourceCreation;

public interface ISourceCreationService
{
  Uri ApiOrigin { get; }
  Task<RssFeedImportSubmission> CreateAsync(Uri url, CancellationToken token);
  Task<CreatedSourceDetail> ResolveAsync(string feedId, CancellationToken token);
  Task<RssFeedImportStatus> ReadAsync(string importId, CancellationToken token);
}

public sealed class SourceCreationService(VouchaApiClient client, HttpClient http) : ISourceCreationService
{
  public Uri ApiOrigin => http.BaseAddress ?? throw new InvalidOperationException();
  public Task<RssFeedImportSubmission> CreateAsync(Uri url, CancellationToken token) =>
      client.SendAsync<RssFeedImportSubmission>(VouchaApiEndpoints.CreateSource(new(url, true)), token);
  public async Task<CreatedSourceDetail> ResolveAsync(string feedId, CancellationToken token) =>
      (await client.SendAsync<CreatedSourceResponse>(new(HttpMethod.Get, $"/api/v1/rss-feeds/{Uri.EscapeDataString(feedId)}"), token).ConfigureAwait(false)).RssFeed;
  public Task<RssFeedImportStatus> ReadAsync(string importId, CancellationToken token) =>
      client.RssFeedImportStatusAsync(importId, token);
}

public sealed record CreatedSourceDetail(string Id, [property: JsonPropertyName("topic_id")] string? TopicId);
public sealed record CreatedSourceResponse([property: JsonPropertyName("rss_feed")] CreatedSourceDetail RssFeed);

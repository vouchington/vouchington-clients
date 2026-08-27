using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task FetchFeatureFlagsAsyncUsesPublicFeatureFlagsEndpoint()
  {
    var handler = new RecordingHandler("""
        {
          "flags": {
            "fediverse": true,
            "memberships": false
          },
          "overrides": {
            "fediverse": true
          }
        }
        """);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.FetchFeatureFlagsAsync(TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/feature-flags");
    Assert.True(response.Flags["fediverse"]);
    Assert.False(response.Flags["memberships"]);
    Assert.True(response.Overrides["fediverse"]);
  }

  [Fact]
  public async Task FetchCaptchaConfigAsyncUsesPublicCaptchaConfigEndpoint()
  {
    var (client, handler) = CreateClient("native.captcha-config.default");

    var response = await client.FetchCaptchaConfigAsync(TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/captcha-config");
    Assert.False(response.AlwaysApprove);
  }

  [Fact]
  public async Task FediverseSearchAsyncUsesSearchEndpoint()
  {
    var handler = new RecordingHandler("""
        {
          "buckets": [
            {
              "provider": "peertube",
              "status": "ok",
              "items": [
                {
                  "provider": "peertube",
                  "result_type": "video",
                  "external_url": "https://videos.example/watch/1",
                  "title": "Test Video",
                  "summary": "A useful video.",
                  "author_name": "Alice",
                  "author_url": "https://videos.example/accounts/alice",
                  "published_at": "2026-07-09T12:34:56Z",
                  "thumbnail_url": "https://videos.example/thumb.jpg",
                  "source_hostname": "videos.example"
                }
              ],
              "next_cursor": "cursor-2"
            }
          ]
        }
        """);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.FediverseSearchAsync(
        query: "test video",
        providers: "peertube",
        type: "video",
        limit: 5,
        after: "cursor-1",
        cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(
        handler,
        HttpMethod.Get,
        "/api/v1/fediverse/search?after=cursor-1&limit=5&providers=peertube&q=test%20video&type=video");
    var bucket = Assert.Single(response.Buckets);
    Assert.Equal("peertube", bucket.Provider);
    Assert.Equal("ok", bucket.Status);
    Assert.Equal("cursor-2", bucket.NextCursor);

    var item = Assert.Single(bucket.Items);
    Assert.Equal("https://videos.example/watch/1", item.Id);
    Assert.Equal("peertube", item.Provider);
    Assert.Equal("video", item.ResultType);
    Assert.Equal(new Uri("https://videos.example/watch/1"), item.ExternalUrl);
    Assert.Equal("Test Video", item.Title);
    Assert.Equal("A useful video.", item.Summary);
    Assert.Equal("Alice", item.AuthorName);
    Assert.Equal(new Uri("https://videos.example/accounts/alice"), item.AuthorUrl);
    Assert.Equal(DateTimeOffset.Parse("2026-07-09T12:34:56Z"), item.PublishedAt);
    Assert.Equal(new Uri("https://videos.example/thumb.jpg"), item.ThumbnailUrl);
    Assert.Equal("videos.example", item.SourceHostname);
  }

  [Fact]
  public void FediverseSearchModelsDeserializeErrorBuckets()
  {
    var response = JsonSerializer.Deserialize<FediverseSearchResponse>(
        """
        {
          "buckets": [
            {
              "provider": "mastodon",
              "status": "error",
              "items": [],
              "error_code": "temporarily unavailable"
            }
          ]
        }
        """,
        VouchaApiJson.Options);

    var bucket = Assert.Single(response!.Buckets);
    Assert.Equal("mastodon", bucket.Provider);
    Assert.Equal("error", bucket.Status);
    Assert.Empty(bucket.Items);
    Assert.Equal("temporarily unavailable", bucket.ErrorCode);
    Assert.Null(bucket.NextCursor);
  }

  [Fact]
  public async Task BeginBlueskyAccountLinkAsyncUsesBlueskyLinkEndpoint()
  {
    var (client, handler) = CreateClient("native.auth.bluesky.link.default");

    var response = await client.BeginBlueskyAccountLinkAsync(
        "alice.bsky.social",
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Post, "/api/v1/auth/bluesky/link");
    Assert.Contains("\"handle\":\"alice.bsky.social\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.Equal(new Uri("https://bsky.social/oauth/authorize"), response.RedirectUrl);
  }

  [Fact]
  public async Task BeginNativeBlueskyAccountLinkAsyncRequestsNativeCallback()
  {
    var (client, handler) = CreateClient("native.auth.bluesky.link.native");

    var response = await client.BeginNativeBlueskyAccountLinkAsync(
        "alice.bsky.social",
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Post, "/api/v1/auth/bluesky/link");
    Assert.Contains("\"callback_mode\":\"native\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.Contains("\"completion_proof_challenge\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.Equal("00000000-0000-7000-8000-00000000b501", response.FlowId);
  }

  [Fact]
  public async Task DisconnectBlueskyAccountAsyncUsesBlueskyLinkEndpoint()
  {
    var (client, handler) = CreateClient("native.auth.bluesky.unlink.default");

    await client.DisconnectBlueskyAccountAsync(TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Delete, "/api/v1/auth/bluesky/link");
  }

  [Fact]
  public async Task FetchFediverseInstancesAsyncUsesDedicatedCursorEndpoint()
  {
    var (client, handler) = CreateClient("native.fediverse.instances.default");

    var response = await client.FetchFediverseInstancesAsync(
        query: "social",
        sort: "best",
        after: "cursor-1",
        limit: 25,
        cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/fediverse/instances?after=cursor-1&limit=25&q=social&sort=best");
    Assert.Equal(2, response.OrderedInstances.Count);
    Assert.Equal("mastodon", response.OrderedInstances[0].Instance?.Software);
    Assert.Equal(10, response.OrderedInstances[0].HostnameElection?.VotesScoreNet);
  }

  [Fact]
  public async Task FetchFediverseInstancesAsyncRetainsTopicWhenInstanceSidecarIsOmitted()
  {
    var fixture = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.fediverse.instances.default"))!;
    const string topicId = "00000000-0000-7000-8000-00000000f001";
    Assert.True(fixture["fediverse_instances"]!.AsObject().Remove(topicId));
    var handler = new RecordingHandler(fixture.ToJsonString());
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.FetchFediverseInstancesAsync(
        cancellationToken: TestContext.Current.CancellationToken);

    var item = Assert.Single(response.OrderedInstances, candidate => candidate.Topic.Id == topicId);
    Assert.Null(item.Instance);
  }

  [Fact]
  public async Task FetchFediverseInstanceAsyncUsesSlugEndpoint()
  {
    var (client, handler) = CreateClient("native.fediverse.instance.slug");

    var response = await client.FetchFediverseInstanceAsync(
        "social-example",
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/fediverse/instances/social-example");
    Assert.Equal("social.example", response.Topic.Name);
    Assert.Equal("activitypub", response.Instance?.Protocol);
  }

  [Fact]
  public async Task CompleteNativeBlueskyAccountLinkAsyncUsesOneTimeCompletionEndpoint()
  {
    var (client, handler) = CreateClient("native.auth.bluesky.link-completion.default");

    await client.CompleteNativeBlueskyAccountLinkAsync(
        "00000000-0000-7000-8000-00000000b501",
        "fixture-completion-token",
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Post, "/api/v1/auth/bluesky/link-completions");
    Assert.Contains("\"flow_id\":\"00000000-0000-7000-8000-00000000b501\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.Contains("\"completion_token\":\"fixture-completion-token\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.Contains("\"completion_proof_verifier\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"", handler.RequestBody, StringComparison.Ordinal);
  }
}

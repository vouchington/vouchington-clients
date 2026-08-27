using System.Net;
using System.Text;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Relations;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class PostComposeApiServicesTests
{
  [Fact]
  public async Task EntityRelationsServiceDelegatesFetchAndCreate()
  {
    var handler = new RecordingHandler([
      new RecordedResponse("""
          {
            "results": [{ "__entity_type": "entity_relation", "id": "relation-1" }],
            "page_info": { "has_next_page": false },
            "entity_relations": {
              "relation-1": { "id": "relation-1", "subject_id": "post-1", "object_id": "url-1" }
            },
            "entity_relation_elections": {
              "relation-1": {
                "__entity_type": "entity_relation_election",
                "id": "relation-1",
                "votes_score_net": 1,
                "votes_count_up": 1,
                "votes_count_down": 0
              }
            },
            "election_votes": { "relation-1": { "choice": "confirm" } }
          }
          """),
      new RecordedResponse("""
          { "relation": { "id": "relation-2", "order_index": 1 } }
          """, HttpStatusCode.Created),
    ]);
    var service = new ApiEntityRelationsService(CreateClient(handler));

    var relations = await service.FetchAsync(
        new EntityRelationsRequest("post", "post-1", "related", "url", MinNetVoteScore: 1),
        TestContext.Current.CancellationToken);
    var created = await service.CreateAsync(
        new CreateEntityRelationRequest("post", "post-1", "related", "url", "url-1"),
        TestContext.Current.CancellationToken);

    Assert.Equal("relation-1", relations.Results[0].Id);
    Assert.Equal(1.0, relations.EntityRelationElections?["relation-1"].VotesScoreNet);
    Assert.Equal(ElectionVoteChoice.Confirm, relations.ElectionVotes?["relation-1"].Choice);
    Assert.Equal("relation-2", created.Relation.Id);
    Assert.Equal(
        "/api/v1/entity-relations/post/post-1/related/url",
        handler.Requests[1].PathAndQuery);
  }

  [Fact]
  public async Task ImageUploadServiceCoversApiCallsAndPresignedPut()
  {
    var apiHandler = new RecordingHandler([
      new RecordedResponse("""
          {
            "upload": {
              "image_id": "image-1",
              "upload_url": "https://s3.example.test/upload",
              "content_type": "image/png",
              "expires_at": "2026-01-01T00:00:00Z"
            }
          }
          """, HttpStatusCode.Created),
      new RecordedResponse("""
          { "image": { "id": "image-1", "upload_status": "processing" } }
          """),
      new RecordedResponse("""
          {
            "upload_state": {
              "id": "image-1",
              "upload_status": "complete",
              "upload_error": null,
              "ready": true,
              "blocked": false
            }
          }
          """),
    ]);
    var putHandler = new PutRecordingHandler();
    var service = new ApiImageUploadService(
        CreateClient(apiHandler),
        new HttpClient(putHandler));

    var upload = await service.CreateUploadUrlAsync(
        new CreateImageUploadUrlBody("image/png", 3),
        TestContext.Current.CancellationToken);
    await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("png"));
    await service.UploadAsync(upload.Upload, stream, 3, TestContext.Current.CancellationToken);
    var completed = await service.CompleteAsync("image-1", TestContext.Current.CancellationToken);
    var state = await service.FetchUploadStateAsync("image-1", TestContext.Current.CancellationToken);

    Assert.Equal("image-1", upload.Upload.ImageId);
    Assert.Equal("image/png", putHandler.ContentType);
    Assert.Equal(3, putHandler.ContentLength);
    Assert.Equal("processing", completed.Image.UploadStatus);
    Assert.True(state.UploadState.Ready);
  }

  [Fact]
  public async Task ImageUploadServiceRejectsInsecureExternalUploadUrlBeforePut()
  {
    var putHandler = new PutRecordingHandler();
    var service = new ApiImageUploadService(
        CreateClient(new RecordingHandler([])),
        new HttpClient(putHandler));
    var upload = new ImageUploadUrl(
        "image-insecure",
        "http://upload.example.test/image-insecure",
        "image/png",
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("png"));

    var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        service.UploadAsync(upload, stream, 3, TestContext.Current.CancellationToken));

    Assert.Equal("Invalid image upload URL.", error.Message);
    Assert.Equal(0, putHandler.SendCount);
  }

  [Fact]
  public void ComposeResponseRecordsExposeJsonBackedData()
  {
    var review = new CommunityPostReview(
        "community-1",
        "post-1",
        DateTimeOffset.UnixEpoch,
        null,
        null);
    var response = new PostMutationResponse(
        new Post("post-1", "discussion", "Title", "Body", "user-1"),
        review);
    var uploadState = new ImageUploadState("image-1", "failed", "bad image", false, true);

    Assert.Equal("community-1", response.CommunityPostReview?.CommunityId);
    Assert.Equal("post-1", response.CommunityPostReview?.PostId);
    Assert.Equal(DateTimeOffset.UnixEpoch, response.CommunityPostReview?.ApprovedAt);
    Assert.Equal("bad image", uploadState.UploadError);
    Assert.True(uploadState.Blocked);
  }

  private static VouchaApiClient CreateClient(HttpMessageHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

  private sealed class PutRecordingHandler : HttpMessageHandler
  {
    public string? ContentType { get; private set; }

    public long? ContentLength { get; private set; }

    public int SendCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      SendCount++;
      ContentType = request.Content?.Headers.ContentType?.MediaType;
      ContentLength = request.Content?.Headers.ContentLength;
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
  }
}

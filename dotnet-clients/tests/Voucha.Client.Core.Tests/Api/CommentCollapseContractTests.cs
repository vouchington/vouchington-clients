using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class CommentCollapseContractTests
{
  [Theory]
  [InlineData("deleted")]
  [InlineData("unmoderated")]
  public void PlaceholderAndDirectChildMetricsDeserializeFromTheLandedSchema(string state)
  {
    var response = JsonSerializer.Deserialize<PostThreadResponse>(
        $$$$"""
        {"results":[{"id":"hidden"}],"page_info":{"has_next_page":false},
         "posts":{"hidden":{"id":"hidden","post_type":"comment","placeholder_state":"{{{{state}}}}","markdown":"","parent_post_id":"root","root_post_id":"root"}},
         "posts_metrics":{"hidden":{"id":"hidden","count":{"children":3,"descendants":8}}},
         "bookmarks":{"hidden":{"hide":true}}}
        """, VouchaApiJson.Options)!;
    Assert.Equal(state, response.Posts["hidden"].PlaceholderState);
    Assert.Equal("root", response.Posts["hidden"].ParentId);
    Assert.Equal(3, response.PostsMetrics!["hidden"].Count.Children);
    Assert.Equal(8, response.PostsMetrics["hidden"].Count.Descendants);
    Assert.True(response.Bookmarks!["hidden"]["hide"]);
  }

  [Fact]
  public async Task CommentServiceSendsHidePutAndDeleteToTheCommentBookmarkEndpoint()
  {
    using var handler = new HideHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiPostsService(new VouchaApiClient(http));
    await service.BookmarkPostAsync("comment", "hide", TestContext.Current.CancellationToken);
    await service.UnbookmarkPostAsync("comment", "hide", TestContext.Current.CancellationToken);
    Assert.Equal([(HttpMethod.Put, "/api/v1/bookmarks/post/comment/hide"), (HttpMethod.Delete, "/api/v1/bookmarks/post/comment/hide")], handler.Calls);
  }

  private sealed class HideHandler : HttpMessageHandler
  {
    public List<(HttpMethod Method, string Path)> Calls { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Calls.Add((request.Method, request.RequestUri!.AbsolutePath));
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }
  }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class RssFeedImportStatusPaginationTests
{
  [Theory]
  [InlineData("native.import-export.rss-feeds.status.retrying")]
  [InlineData("native.import-export.rss-feeds.status.partial")]
  public void CurrentStatusFixturesRetainPageInfo(string fixtureId)
  {
    var json = ApiFixtureLoader.LoadResponse(fixtureId);
    var status = JsonSerializer.Deserialize<RssFeedImportStatus>(json, VouchaApiJson.Options);

    Assert.NotNull(status);
    Assert.NotNull(status.PageInfo);
    Assert.False(status.PageInfo.HasNextPage);
    Assert.Null(status.PageInfo.EndCursor);
    Assert.NotNull(status.PageInfo.StartCursor);
    if (fixtureId == "native.import-export.rss-feeds.status.retrying")
    {
      Assert.Equal(2, status.Import.PendingRows);
      Assert.Contains(status.Rows, row => row.Status == ImportResultStatus.Pending && row.Error is not null);
    }
    else
    {
      Assert.NotNull(status.Import.CompletedAt);
      Assert.Equal(0, status.Import.PendingRows);
    }

    using var encoded = JsonDocument.Parse(JsonSerializer.Serialize(status, VouchaApiJson.Options));
    Assert.Equal(
        status.PageInfo.StartCursor,
        encoded.RootElement.GetProperty("page_info").GetProperty("start_cursor").GetString());
  }

  [Fact]
  public void ContinuationCursorRemainsOpaqueAcrossDecodeAndEncode()
  {
    var statusJson = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.import-export.rss-feeds.status.retrying"))!.AsObject();
    var pageInfo = statusJson["page_info"]!.AsObject();
    var cursor = pageInfo["start_cursor"]!.GetValue<string>();
    pageInfo["start_cursor"] = null;
    pageInfo["end_cursor"] = cursor;
    pageInfo["has_next_page"] = true;

    var status = JsonSerializer.Deserialize<RssFeedImportStatus>(statusJson.ToJsonString(), VouchaApiJson.Options);

    Assert.NotNull(status);
    Assert.NotNull(status.PageInfo);
    Assert.True(status.PageInfo.HasNextPage);
    Assert.Equal(cursor, status.PageInfo.EndCursor);

    using var encoded = JsonDocument.Parse(JsonSerializer.Serialize(status, VouchaApiJson.Options));
    var encodedPageInfo = encoded.RootElement.GetProperty("page_info");
    Assert.True(encodedPageInfo.GetProperty("has_next_page").GetBoolean());
    Assert.Equal(cursor, encodedPageInfo.GetProperty("end_cursor").GetString());
  }
}

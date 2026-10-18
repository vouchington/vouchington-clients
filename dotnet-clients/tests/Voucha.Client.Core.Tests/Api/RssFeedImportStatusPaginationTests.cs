using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Localization;
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
      Assert.Contains(status.Rows, row => row.Status == ImportResultStatus.Pending && row.ErrorCode == RssFeedImportErrorCode.ImportFailed);
    }
    else
    {
      Assert.Equal("local-news", status.Rows[0].TopicSlug);
      Assert.Equal("70000000-0000-7000-8000-000000000004", status.Rows[0].EntityId);
      Assert.Equal(ImportResultStatus.Followed, status.Rows[0].Status);
      Assert.Equal(RssFeedImportErrorCode.ImportFailed, status.Rows[1].ErrorCode);
      Assert.Equal(ImportResultStatus.Error, status.Rows[1].Status);
      Assert.NotNull(status.Import.CompletedAt);
      Assert.Equal(0, status.Import.PendingRows);
    }

    using var encoded = JsonDocument.Parse(JsonSerializer.Serialize(status, VouchaApiJson.Options));
    Assert.Equal(
        status.PageInfo.StartCursor,
        encoded.RootElement.GetProperty("page_info").GetProperty("start_cursor").GetString());
  }

  [Fact]
  public void CanonicalPendingErrorCodeRendersRetryingWithoutBecomingTerminal()
  {
    var status = JsonSerializer.Deserialize<RssFeedImportStatus>(
        ApiFixtureLoader.LoadResponse("native.import-export.rss-feeds.status.retrying"), VouchaApiJson.Options)!;
    var retrying = status.Rows[0];
    Assert.Null(retrying.Error);
    Assert.True(retrying.IsRetrying());
    Assert.False(retrying.IsTerminalFailure());
    Assert.Equal("Retrying", retrying.DisplayStatus);
    var localization = UiLocalization.English;
    var presentation = new ImportResultPresentation(retrying, localization);
    Assert.Equal(
        localization.Format(UiMessageKey.NativeSwiftImportExportRetrying,
            ("error", localization.Localize(UiMessageKey.NativeDotnetCsharpActionFailed))),
        presentation.LocalizedDisplayStatus);
    Assert.Null(presentation.ExternalContentError);
    Assert.False(status.Rows[1].IsRetrying());
    Assert.Equal("Pending", status.Rows[1].DisplayStatus);
  }

  [Theory]
  [InlineData("INVALID_URL", RssFeedImportErrorCode.InvalidUrl)]
  [InlineData("INVALID_FEED_CONTENT", RssFeedImportErrorCode.InvalidFeedContent)]
  [InlineData("FEED_FETCH_TIMEOUT", RssFeedImportErrorCode.FeedFetchTimeout)]
  [InlineData("CONTRIBUTION_LIMIT_EXCEEDED", RssFeedImportErrorCode.ContributionLimitExceeded)]
  [InlineData("IMPORT_FAILED", RssFeedImportErrorCode.ImportFailed)]
  public void ErrorCodesPreserveCanonicalWireValues(string wire, RssFeedImportErrorCode expected)
  {
    var json = JsonSerializer.Serialize(wire);
    Assert.Equal(expected, JsonSerializer.Deserialize<RssFeedImportErrorCode>(json, VouchaApiJson.Options));
    Assert.Equal(json, JsonSerializer.Serialize(expected, VouchaApiJson.Options));
  }

  [Fact]
  public void UnknownErrorCodeIsRejected()
  {
    Assert.Throws<JsonException>(() =>
        JsonSerializer.Deserialize<RssFeedImportErrorCode>("\"UNKNOWN\"", VouchaApiJson.Options));
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

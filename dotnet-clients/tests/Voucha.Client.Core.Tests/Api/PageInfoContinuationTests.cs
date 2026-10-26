using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class PageInfoContinuationTests
{
  [Fact]
  public void CombinesNextPageWithOptionalHasMore()
  {
    Assert.True(new PageInfo(null, true, null).HasMorePages);
    Assert.True(new PageInfo(null, false, null, true).HasMorePages);
    Assert.True(new PageInfo(null, true, null, false).HasMorePages);
    Assert.False(new PageInfo(null, false, null).HasMorePages);
    Assert.False(new PageInfo(null, false, null, false).HasMorePages);
  }

  [Fact]
  public void DecodedHasMoreAloneContinuesThePage()
  {
    var page = JsonSerializer.Deserialize<PageInfo>(
        """{"has_next_page":false,"has_more":true}""",
        VouchaApiJson.Options);

    Assert.NotNull(page);
    Assert.False(page.HasNextPage);
    Assert.Equal(true, page.HasMore);
    Assert.True(page.HasMorePages);
  }

  [Fact]
  public void SerializedPageInfoOmitsDerivedContinuation()
  {
    var json = JsonSerializer.Serialize(
        new PageInfo("cursor", false, null, true),
        VouchaApiJson.Options);

    Assert.Contains("\"has_more\":true", json, StringComparison.Ordinal);
    Assert.DoesNotContain("hasMorePages", json, StringComparison.Ordinal);
    Assert.DoesNotContain("HasMorePages", json, StringComparison.Ordinal);
  }
}

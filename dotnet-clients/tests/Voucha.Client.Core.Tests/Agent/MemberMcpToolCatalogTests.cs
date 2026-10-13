using System.Text.Json;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpToolCatalogTests
{
  [Fact]
  public async Task FollowsOpaqueCursorAndPublishesCompleteCatalog()
  {
    var cursors = new List<string?>();
    var pages = new[]
    {
      JsonDocument.Parse("""{"tools":[{"name":"first"}],"nextCursor":"opaque/+","version":"v1"}""").RootElement,
      JsonDocument.Parse("""{"tools":[{"name":"second"}]}""").RootElement
    };
    var catalog = await MemberMcpToolCatalog.ListAllAsync((cursor, _) =>
    {
      cursors.Add(cursor);
      return Task.FromResult(pages[cursors.Count - 1]);
    }, TestContext.Current.CancellationToken);

    Assert.Equal([null, "opaque/+"], cursors);
    Assert.Equal(["first", "second"], catalog.GetProperty("tools").EnumerateArray()
        .Select(tool => tool.GetProperty("name").GetString()));
    Assert.Equal("v1", catalog.GetProperty("version").GetString());
    Assert.False(catalog.TryGetProperty("nextCursor", out _));
  }

  [Theory]
  [InlineData("""{"tools":[],"nextCursor":"repeat"}""", """{"tools":[],"nextCursor":"repeat"}""")]
  [InlineData("""{"tools":[],"nextCursor":3}""", null)]
  [InlineData("""{"tools":{}}""", null)]
  public async Task RejectsMalformedOrCyclicPages(string first, string? second)
  {
    var pages = new[] { first, second };
    var requests = 0;
    await Assert.ThrowsAsync<InvalidDataException>(() => MemberMcpToolCatalog.ListAllAsync((_, _) =>
    {
      var page = JsonDocument.Parse(Assert.IsType<string>(pages[requests++]));
      return Task.FromResult(page.RootElement);
    }, TestContext.Current.CancellationToken));
    Assert.Equal(second is null ? 1 : 2, requests);
  }

  [Fact]
  public async Task RejectsCatalogThatExceedsToolBoundBeforePublication()
  {
    var page = JsonSerializer.SerializeToElement(new
    {
      tools = Enumerable.Range(0, 1_025).Select(index => new { name = $"tool-{index}" })
    });
    var requests = 0;

    await Assert.ThrowsAsync<InvalidDataException>(() => MemberMcpToolCatalog.ListAllAsync((_, _) =>
    {
      requests++;
      return Task.FromResult(page);
    }, TestContext.Current.CancellationToken));
    Assert.Equal(1, requests);
  }
}

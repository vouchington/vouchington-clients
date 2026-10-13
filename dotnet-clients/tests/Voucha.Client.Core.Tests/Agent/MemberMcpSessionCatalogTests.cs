using System.Text.Json;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Fact]
  public async Task OversizedCatalogNeverPublishesAConnectedSession()
  {
    var factory = new FakeFactory { Catalog = Catalog(129) };
    using var session = new MemberMcpSession(
        new FakeSessionStore("account-a"), new FakeStore(), factory);

    await Assert.ThrowsAsync<InvalidDataException>(() =>
        session.ConnectAsync(TestContext.Current.CancellationToken));

    Assert.Equal(MemberMcpSessionStatus.Error, session.Status);
    Assert.Null(session.ConnectedSnapshot());
    Assert.Null(session.Tools);
  }

  [Fact]
  public async Task InvalidRefreshedCatalogClearsEarlierToolProjection()
  {
    var factory = new FakeFactory { Catalog = Catalog(128) };
    using var session = new MemberMcpSession(
        new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    Assert.Equal(128, session.ConnectedSnapshot()?.Catalog.GetProperty("tools").GetArrayLength());

    factory.Connections[0].Catalog = Catalog(129);
    await Assert.ThrowsAsync<InvalidDataException>(() =>
        session.RefreshToolsAsync(TestContext.Current.CancellationToken));
    await session.WhenSettledAsync();

    Assert.Null(session.ConnectedSnapshot());
    Assert.Null(session.Tools);
    Assert.Equal(MemberMcpSessionStatus.Disconnected, session.Status);
  }

  private static JsonElement Catalog(int count) => JsonSerializer.SerializeToElement(new
  {
    tools = Enumerable.Range(0, count).Select(index => new
    {
      name = $"tool-{index}",
      inputSchema = new { type = "object" },
    }),
  });
}

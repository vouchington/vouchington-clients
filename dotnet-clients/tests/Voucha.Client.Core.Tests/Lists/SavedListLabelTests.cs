using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Lists;

public sealed partial class ListsViewModelTests
{
  [Fact]
  public async Task FixtureLabelsReachNativeRowsIncludingLocalizedUntitledPost()
  {
    var (model, _) = CreateViewModel(
        ApiFixtureLoader.LoadResponse("native.lists.default"),
        ApiFixtureLoader.LoadResponse("native.list-items.default"));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Saved article", model.Items[0].EntityLabel);
    var untitled = Assert.Single(model.Items, row => row.ProtocolItemType == "post");
    Assert.Equal("Untitled post", untitled.EntityLabel);
    Assert.Equal(UiMessageKey.NativeSwiftModerationReportsReviewQueueUntitledPost, untitled.EntityLabelText.Key);
    Assert.NotEqual(untitled.EntityId, untitled.EntityLabel);
  }

  [Fact]
  public void ListReadAndAddRequireNullableLabelFact()
  {
    var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.list-items.default"))!;
    var item = body["list_items"]!.AsObject().First().Value!.DeepClone();
    var added = JsonSerializer.Deserialize<ListItemResponse>(new JsonObject { ["list_item"] = item }.ToJsonString(), VouchaApiJson.Options)!;
    Assert.Equal("Saved article", added.ListItem.EntityLabel);
    item.AsObject().Remove("entity_label");
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ListItemResponse>(new JsonObject { ["list_item"] = item.DeepClone() }.ToJsonString(), VouchaApiJson.Options));
    body["list_items"]!.AsObject().First().Value!.AsObject().Remove("entity_label");
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ListItemsResponse>(body.ToJsonString(), VouchaApiJson.Options));
  }
}

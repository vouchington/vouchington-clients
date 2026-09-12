using System.Text.Json;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Localization;

public sealed class LocalizationLeafFlattenTests
{
  [Fact]
  public void FlattenExpandsStringPluralAndSelectLeaves()
  {
    using var document = JsonDocument.Parse("""
        {
          "common.cancel": "Abort",
          "shared.count": {
            "kind": "plural",
            "valueParameter": "count",
            "forms": { "one": "{count} item", "other": "{count} items" }
          },
          "shared.label": {
            "kind": "select-plural",
            "cases": {
              "post": { "one": "{count} post", "other": "{count} posts" }
            }
          }
        }
        """);
    var messages = document.RootElement.EnumerateObject()
        .ToDictionary(property => property.Name, property => property.Value.Clone());

    var values = LocalizationLeafFlatten.Flatten(messages);

    Assert.Equal("Abort", values["common.cancel"]);
    Assert.Equal("{count} item", values["shared.count.__plural.one"]);
    Assert.Equal("{count} items", values["shared.count.__plural.other"]);
    Assert.Equal("{count} post", values["shared.label.__select.post.one"]);
    Assert.Equal("{count} posts", values["shared.label.__select.post.other"]);
  }
}

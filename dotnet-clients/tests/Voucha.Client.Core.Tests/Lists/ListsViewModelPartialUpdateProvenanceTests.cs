using System.Text.Json.Nodes;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Lists;

public sealed partial class ListsViewModelTests
{
  [Fact]
  public async Task PartialListUpdatePreservesCachedCreationFacts()
  {
    var fixture = JsonNode.Parse(ApiFixtureLoader.LoadResponse("entity-provenance.lists.staff"))!;
    var updatedList = fixture["lists"]!["list-1-mcp"]!.DeepClone().AsObject();
    updatedList.Remove("provenance");
    updatedList.Remove("staff_provenance");
    updatedList["name"] = "Renamed Queue";
    var patchResponse = new JsonObject { ["list"] = updatedList }.ToJsonString();
    var (viewModel, _) = CreateViewModel(
        ApiFixtureLoader.LoadResponse("entity-provenance.lists.staff"),
        ApiFixtureLoader.LoadResponse("native.list-items.default"),
        patchResponse);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(viewModel.SelectedList?.Provenance);
    Assert.NotNull(viewModel.SelectedList?.StaffProvenance);

    await viewModel.UpdateSelectedListAsync("Renamed Queue", null, TestContext.Current.CancellationToken);

    Assert.Equal("Renamed Queue", viewModel.SelectedList?.Name);
    Assert.NotNull(viewModel.SelectedList?.Provenance);
    Assert.NotNull(viewModel.SelectedList?.StaffProvenance);
    var collectionRow = Assert.Single(viewModel.Lists, list => list.Id == "list-1-mcp");
    Assert.Equal("Renamed Queue", collectionRow.Name);
    Assert.NotNull(collectionRow.Provenance);
    Assert.NotNull(collectionRow.StaffProvenance);
  }
}

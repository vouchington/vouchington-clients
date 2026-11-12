using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class PostTimeRangePickerRenderingTests
{
  [Fact]
  public void NativePickerRendersLocalizedOptionsAndEmitsSelectedProtocolRange()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    app.Resources["extracted.posts.postTimeRangeSelect.label"] = "Time range";
    var model = new PostTimeRangeSelection(UiLocalization.English);
    var control = new PostTimeRangePicker { Selection = model };
    var picker = control.FindByName<Picker>("RangePicker");
    var emitted = new List<string>();
    control.RangeSelected += (_, value) =>
    {
      emitted.Add(value);
      model.Select(value);
    };

    Assert.Equal("post-time-range", picker.AutomationId);
    Assert.Equal("Time range", picker.Title);
    Assert.Equal("Time range", SemanticProperties.GetDescription(picker));
    Assert.Equal(new[] { "Last 7 days", "Last 30 days", "Last year", "All time" }, picker.Items);
    Assert.Equal(1, picker.SelectedIndex);
    Assert.Empty(emitted);

    picker.SelectedIndex = 3;

    Assert.Equal(new[] { "all" }, emitted);
    Assert.Equal("all", model.RequestValue);
    picker.SelectedIndex = 1;
    Assert.Equal(new[] { "all", "1m" }, emitted);
    Assert.Equal("1m", model.RequestValue);
  }

  [Fact]
  public void ReconfiguredNativePickerDropsAllAndReselectsYearWithoutEmittingUserInput()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var model = new PostTimeRangeSelection(UiLocalization.English);
    var control = new PostTimeRangePicker { Selection = model };
    var picker = control.FindByName<Picker>("RangePicker");
    var emitted = new List<string>();
    control.RangeSelected += (_, value) =>
    {
      emitted.Add(value);
      model.Select(value);
    };
    model.Reconfigure(["1w", "1m", "1y"], "1y", preserveSelection: false);
    Assert.Equal(new[] { "Last 7 days", "Last 30 days", "Last year" }, picker.Items);
    Assert.Equal(2, picker.SelectedIndex);
    Assert.Equal("1y", model.RequestValue);
    Assert.Empty(emitted);
    picker.SelectedIndex = 1;
    Assert.Equal(new[] { "1m" }, emitted);
    Assert.Equal("1m", model.RequestValue);
    model.Reconfigure(PostTimeRangeSelection.BrowseOptions, "1m");
    Assert.Equal(1, picker.SelectedIndex);
    Assert.Equal("All time", picker.Items[3]);
    Assert.Equal(new[] { "1m" }, emitted);
  }

  [Fact]
  public void InputOptionsCanRestrictToYearDefaultAndVisibleLabelsRefreshWithoutSelectionChange()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    app.Resources["extracted.posts.postTimeRangeSelect.label"] = localization.Localize(
        UiMessageKey.ExtractedPostsPostTimeRangeSelectLabel);
    var model = new PostTimeRangeSelection(localization, ["1w", "1m", "1y"], "1y");
    var control = new PostTimeRangePicker { Selection = model };
    var picker = control.FindByName<Picker>("RangePicker");
    var emitted = new List<string>();
    control.RangeSelected += (_, value) => emitted.Add(value);
    Assert.Equal(new[] { "Last 7 days", "Last 30 days", "Last year" }, picker.Items);
    Assert.Equal(2, picker.SelectedIndex);

    controller.ApplySavedLocale("es");
    app.Resources["extracted.posts.postTimeRangeSelect.label"] = localization.Localize(
        UiMessageKey.ExtractedPostsPostTimeRangeSelectLabel);
    model.RefreshLabels();

    Assert.Equal("Período", picker.Title);
    Assert.Equal("Último año", picker.Items[2]);
    Assert.Equal(2, picker.SelectedIndex);
    Assert.Equal("1y", model.RequestValue);
    Assert.Empty(emitted);
  }
}

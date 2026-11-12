using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostTimeRangeSelectionTests
{
  [Fact]
  public void BrowseOptionsHaveLocalizedKeysAndDefaultMonthSendsExplicitWindow()
  {
    var selection = new PostTimeRangeSelection(UiLocalization.English);
    Assert.Equal(new[] { "1w", "1m", "1y", "all" }, selection.Options.Select(option => option.Value));
    Assert.Equal(UiMessageKey.ExtractedPostsPostTimeRangeSelectLabel, selection.LabelText.Key);
    Assert.Equal(new[]
    {
      UiMessageKey.ExtractedPostsPostTimeRangeSelectLast7Days,
      UiMessageKey.ExtractedPostsPostTimeRangeSelectLast30Days,
      UiMessageKey.ExtractedPostsPostTimeRangeSelectLastYear,
      UiMessageKey.ExtractedPostsPostTimeRangeSelectAllTime,
    }, selection.Options.Select(option => option.LabelText.Key!.Value));
    Assert.Equal("1m", selection.SelectedOption.Value);
    Assert.Equal("1m", selection.RequestValue);
    Assert.False(selection.Select("1m"));
    Assert.True(selection.Select("all"));
    Assert.Equal("all", selection.RequestValue);
    Assert.True(selection.Select("1m"));
    Assert.Equal("1m", selection.RequestValue);
  }

  [Fact]
  public void RestrictedOptionsDefaultToExplicitYearAndRejectAllTime()
  {
    var selection = new PostTimeRangeSelection(UiLocalization.English, ["1w", "1m", "1y"], "1y");
    Assert.Equal("1y", selection.SelectedOption.Value);
    Assert.Equal("1y", selection.RequestValue);
    Assert.Throws<ArgumentException>(() => selection.Select("all"));
    Assert.Equal(new[] { "1w", "1m", "1y" }, selection.Options.Select(option => option.Value));
    selection.Select("1w");
    Assert.Equal("1w", selection.RequestValue);
  }

  [Fact]
  public void InvalidOptionsFailClosed()
  {
    Assert.Throws<ArgumentException>(() => new PostTimeRangeSelection(UiLocalization.English, ["1d"]));
    Assert.Throws<ArgumentException>(() => new PostTimeRangeSelection(UiLocalization.English, ["1w"]));
  }

  [Fact]
  public void LocaleRefreshKeepsValueAndUpdatesVisibleOptions()
  {
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var selection = new PostTimeRangeSelection(new UiLocalization(controller));
    selection.Select("1y");
    var labels = selection.Options.Select(option => option.Label).ToArray();
    controller.ApplySavedLocale("es");
    selection.RefreshLabels();
    Assert.Equal("1y", selection.SelectedOption.Value);
    Assert.Equal("1y", selection.RequestValue);
    Assert.All(selection.Options.Zip(labels), pair => Assert.NotEqual(pair.Second, pair.First.Label));
  }
}

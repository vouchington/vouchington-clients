using Microsoft.Maui.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class IntegrityPageViewsTests
{
  [Fact]
  public void PendingResolutionShowsOnlyThePendingLabel()
  {
    using var scope = UiCopy.PushLocalization(UiLocalization.English);
    var content = new VerticalStackLayout();

    IntegrityPageViews.AddResolution(content, null, null, null);

    var label = Assert.Single(content.Children.OfType<Label>());
    Assert.Equal(UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityPending), label.Text);
  }

  [Fact]
  public void PresentResolutionFieldsSuppressThePendingLabel()
  {
    using var scope = UiCopy.PushLocalization(UiLocalization.English);
    var resolvedAt = new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);
    var content = new VerticalStackLayout();

    IntegrityPageViews.AddResolution(content, "penalized", resolvedAt, "admin-1");

    Assert.Equal(
        [
          UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityResolution),
          UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityPenalizedResolution),
          UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityResolvedAt),
          UiCopy.FormatDateTime(resolvedAt),
          UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityResolvedBy),
          "admin-1",
        ],
        content.Children.OfType<Label>().Select(label => label.Text).ToArray());
  }

  [Fact]
  public void AResolverAloneSuppressesThePendingLabel()
  {
    using var scope = UiCopy.PushLocalization(UiLocalization.English);
    var content = new VerticalStackLayout();

    IntegrityPageViews.AddResolution(content, null, null, "admin-1");

    Assert.Equal(
        [
          UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityResolvedBy),
          "admin-1",
        ],
        content.Children.OfType<Label>().Select(label => label.Text).ToArray());
  }
}

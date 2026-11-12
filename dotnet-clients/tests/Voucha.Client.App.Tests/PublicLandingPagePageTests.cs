using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Tests.LandingPages;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class PublicLandingPagePageTests
{
  [Fact]
  public void RenderedItemActivationsNavigateOnceAndGroupHeadingHasNoAction()
  {
    var session = new PublicLandingSession();
    var signals = new PublicLandingSignals();
    using var model = new PublicLandingPageViewModel(_ => Task.FromResult(PublicLandingPageTestData.Response()), session, signals);
    var opened = new List<PublicLandingPageTarget>();
    using var page = CreatePage(model, opened);
    Lifecycle(page, "OnAppearing");
    var layout = Assert.IsType<VerticalStackLayout>(Assert.IsType<ScrollView>(page.Content).Content);
    Assert.Equal("My Links", page.Title);
    Assert.Single(signals.Visits);
    var heading = Assert.Single(layout.Children.OfType<Label>(), label => label.Text == "Travel Cards");
    Assert.Empty(heading.GestureRecognizers);
    Button(page, "group-entry-review-1").SendClicked();
    Assert.Equal("travel-card-benefits", Assert.Single(opened).ReviewIdOrSlug);
    Assert.Equal("item-topic-group-1", Assert.Single(signals.Clicks).Signal.LandingPageItemId);
    Assert.Equal("group-entry-review-1", signals.Clicks[0].Signal.GroupMemberId);
    Button(page, "item-profile-1").SendClicked();
    Button(page, "item-referral-1").SendClicked();
    Button(page, "item-link-1").SendClicked();
    Assert.Equal(4, opened.Count);
    Assert.Equal(4, signals.Clicks.Count);
    Assert.Equal("https://example.com/newsletter", opened[3].ExternalUrl?.ToString());
    Assert.Single(signals.Visits);
  }

  [Fact]
  public async Task LifecycleAndReloadSuppressDuplicatesAndInactiveButtonsDoNotSend()
  {
    var session = new PublicLandingSession();
    var signals = new PublicLandingSignals();
    using var model = new PublicLandingPageViewModel(_ => Task.FromResult(PublicLandingPageTestData.Response()), session, signals);
    var opened = new List<PublicLandingPageTarget>();
    using var page = CreatePage(model, opened);
    Lifecycle(page, "OnAppearing");
    Lifecycle(page, "OnAppearing");
    await model.ReloadAsync(TestContext.Current.CancellationToken);
    Assert.Single(signals.Visits);
    var oldButton = Button(page, "item-link-1");
    Lifecycle(page, "OnDisappearing");
    oldButton.SendClicked();
    Assert.Empty(signals.Clicks);
    Assert.Empty(opened);
    Lifecycle(page, "OnAppearing");
    Assert.Equal(2, signals.Visits.Count);
    session.Set(null);
    oldButton.SendClicked();
    Assert.Empty(opened);
    Assert.Equal("My Links", page.Title);
    Button(page, "item-link-1").SendClicked();
    Assert.Single(opened);
    Assert.Empty(signals.Clicks);
    Assert.Equal(2, signals.Visits.Count);
  }

  private static PublicLandingPagePage CreatePage(PublicLandingPageViewModel model, List<PublicLandingPageTarget> opened)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    return new PublicLandingPagePage(model, target => { opened.Add(target); return Task.CompletedTask; });
  }

  private static Button Button(PublicLandingPagePage page, string id) => Assert.Single(
      ((VerticalStackLayout)((ScrollView)page.Content!).Content).Children.OfType<Button>(),
      button => button.AutomationId == $"public-landing-item-{id}");

  private static void Lifecycle(PublicLandingPagePage page, string method) => typeof(PublicLandingPagePage)
      .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null);
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Households;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Households;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class HouseholdInvitationsPageTests
{
  [Fact]
  public void ReceivedRouteFactoryOpensActualReceivedPageAfterSignIn()
  {
    using var model = new HouseholdInvitationsViewModel(new HouseholdInvitationsTestService());
    var page = CreatePage(model);
    using var services = new ServiceCollection().AddSingleton(page).BuildServiceProvider();
    var anonymous = NativeDeepLinkResolver.Resolve("voucha://my/household-invitations", NavigationViewer.Anonymous);
    Assert.Null(HouseholdRoutePageFactory.Create(services, anonymous));
    var signedIn = NativeDeepLinkResolver.Resolve("voucha://my/household-invitations", new NavigationViewer(true, []));
    Assert.Same(page, HouseholdRoutePageFactory.Create(services, signedIn));
    Assert.True(model.IsReceived);
  }

  [Theory]
  [InlineData("accept")]
  [InlineData("decline")]
  public async Task ReceivedListRendersUsernamesAndRunsDecision(string action)
  {
    var invitation = HouseholdInvitationsTestService.Invitation();
    var service = new HouseholdInvitationsTestService { Fetch = _ => Task.FromResult(HouseholdInvitationsTestService.Page(invitation)) };
    using var model = new HouseholdInvitationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(model);
    Assert.Contains(VisualTreeElements.Descendants<Label>(page), label => label.Text == "Invited by @ownername");
    Assert.Empty(VisualTreeElements.Descendants<WebView>(page));
    var key = action == "accept" ? UiMessageKey.SettingsHouseholdInvitationsAccept : UiMessageKey.SettingsHouseholdInvitationsDecline;
    Button(page, key).SendClicked();
    await PageTestWait.WaitUntilAsync(() => service.Actions.Contains(action));
    await PageTestWait.WaitUntilAsync(() => model.Rows.Count == 0);
    Assert.True(model.ShowsEmpty);
  }

  [Fact]
  public async Task OwnerFormSendsUsernameAndRelationshipAndRevokesRenderedRow()
  {
    var invitation = HouseholdInvitationsTestService.Invitation();
    var service = new HouseholdInvitationsTestService { Fetch = _ => Task.FromResult(HouseholdInvitationsTestService.Page(invitation)) };
    using var model = new HouseholdInvitationsViewModel(service);
    var page = CreatePage(model);
    page.ConfigureSent("home");
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Contains(VisualTreeElements.Descendants<Label>(page), label => label.Text == "Invitation for @invitee");
    page.FindByName<Entry>("UsernameEntry").Text = "new-member";
    page.FindByName<Entry>("RelationshipEntry").Text = "partner";
    Button(page, UiMessageKey.SettingsHouseholdInvitationsSendInvitation).SendClicked();
    await PageTestWait.WaitUntilAsync(() => page.FindByName<Entry>("UsernameEntry").Text == string.Empty);
    Assert.Equal("new-member", service.Username);
    Assert.Equal("partner", service.Relationship);
    Button(page, UiMessageKey.SettingsHouseholdInvitationsRevokeInvitation).SendClicked();
    await PageTestWait.WaitUntilAsync(() => model.Rows.Count == 0);
    Assert.Contains("revoke", service.Actions);
  }

  [Fact]
  public async Task LoadErrorRendersRetryAndRetriesTheFailedCursor()
  {
    var service = new HouseholdInvitationsTestService { Fetch = _ => throw new HttpRequestException("offline") };
    using var model = new HouseholdInvitationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(model);
    Assert.Contains(VisualTreeElements.Descendants<Label>(page), label => label.Text == model.LoadError);
    service.Fetch = _ => Task.FromResult(HouseholdInvitationsTestService.Page());
    Button(page, UiMessageKey.SettingsHouseholdInvitationsRetry).SendClicked();
    await PageTestWait.WaitUntilAsync(() => model.ShowsEmpty);
    Assert.False(model.HasError);
    Assert.Equal(2, service.Requests.Count);
  }

  private static Button Button(ContentPage page, UiMessageKey key) =>
      Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.Text == UiLocalization.English.Localize(key));

  private static HouseholdInvitationsPage CreatePage(HouseholdInvitationsViewModel model)
  {
    DispatcherProvider.SetCurrent(new ImmediateTimerDispatcherProvider(ImmediateTimerStart.RemainRunningWhenRepeating));
    var application = new Application();
    foreach (var key in UiMessageKey.All) application.Resources[key.Value] = UiLocalization.English.Localize(key);
    return new HouseholdInvitationsPage(model);
  }
}

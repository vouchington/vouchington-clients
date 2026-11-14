using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ModerationModalActionTests
{
  [Theory]
  [InlineData("save")]
  [InlineData("approve")]
  [InlineData("deliver")]
  [InlineData("rerun")]
  [InlineData("refreshAIRerun")]
  [InlineData("refreshDelivery")]
  [InlineData("accept")]
  [InlineData("reduce")]
  [InlineData("deny")]
  public async Task ClosingAppealModalContainsCancellationFromEveryActionHandler(string action)
  {
    Prepare();
    var service = new ModerationModalAppealsService();
    service.Current = service.Current with
    {
      PublicResponse = "Ready response",
      ApprovedAt = action is "deliver" or "refreshDelivery" ? DateTimeOffset.UtcNow : null,
      SentAt = action is "accept" or "reduce" or "deny" ? DateTimeOffset.UtcNow : null,
    };
    using var model = new ModerationAppealsViewModel(service, new NavigationViewer(true, ["administrator"], IdentityId: "staff-1"));
    var page = AppealPage(model, service.Current.Id);
    Lifecycle(page, "OnAppearing");
    var card = Assert.Single(VisualTreeElements.Descendants<ModerationAppealCardView>(page));
    Loaded(card);
    if (action == "refreshAIRerun")
    {
      service.Mode = "seed-ai";
      await model.RerunAsync(model.Appeals.Single(), TestContext.Current.CancellationToken);
      Assert.True(model.IsAIRerunPending(model.Appeals.Single()));
    }
    if (action == "refreshDelivery")
    {
      service.Mode = "seed-delivery";
      await model.DeliverAsync(model.Appeals.Single(), TestContext.Current.CancellationToken);
      Assert.True(model.IsDeliveryAmbiguous(model.Appeals.Single()));
    }
    if (action == "save") Assert.Single(card.Children.OfType<Editor>()).Text = "Edited response";
    service.Mode = "block";
    var button = Find<Button>(page, $"moderation-appeal-{action}");
    Assert.True(button.IsEnabled);
    button.SendClicked();
    await service.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

    Lifecycle(page, "OnDisappearing");

    await service.CancellationObserved.Task.WaitAsync(TestContext.Current.CancellationToken);
    await WaitUntilAsync(() => !model.IsMutating);
    await Task.Yield();
  }

  [Fact]
  public async Task FailedAppealSaveShowsItsReasonInModalAndClearsItOnAccountChange()
  {
    Prepare();
    var owner = new NavigationViewer(true, ["administrator"], IdentityId: "staff-1");
    var provider = new MutableNavigationViewerProvider();
    provider.SetViewer(owner);
    var service = new ModerationModalAppealsService();
    using var model = new ModerationAppealsViewModel(service, owner, viewerProvider: provider);
    var page = AppealPage(model, service.Current.Id);
    Lifecycle(page, "OnAppearing");
    var card = Assert.Single(VisualTreeElements.Descendants<ModerationAppealCardView>(page));
    Loaded(card);
    Assert.Single(card.Children.OfType<Editor>()).Text = "Edited response";
    service.Mode = "reject";

    Find<Button>(page, "moderation-appeal-save").SendClicked();

    await WaitUntilAsync(() => model.HasError && !model.IsMutating);
    var error = Find<Label>(page, "moderation-appeal-action-error");
    Assert.True(error.IsVisible);
    Assert.Equal(model.ErrorMessage, error.Text);
    Assert.Equal("The server rejected this edited response.", error.Text);
    provider.SetViewer(new NavigationViewer(true, ["administrator"], IdentityId: "staff-2"));
    Assert.Empty(card.Children);
    Assert.Null(card.BindingContext);
    Assert.Null(error.Text);
    Assert.False(error.IsVisible);
    Lifecycle(page, "OnDisappearing");
  }

  [Fact]
  public async Task FailedDisputeSaveShowsLocalizedModalErrorAndClearsItWithBinding()
  {
    Prepare();
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("es"));
    using var serviceLocalization = UiCopy.PushLocalization(new UiLocalization(locale));
    var service = new ModerationModalDisputesService();
    using var model = new ModerationDisputesViewModel(service, new NavigationViewer(true, ["moderator"]), locale);
    var page = new ModerationDetailPage<ModerationDispute>(token => model.FetchDetailAsync(service.Current.Id, token),
        (detail, token) => new ModerationDisputeCardView(model, () => token) { BindingContext = detail });
    Lifecycle(page, "OnAppearing");
    var card = Assert.Single(VisualTreeElements.Descendants<ModerationDisputeCardView>(page));
    Loaded(card);
    card.Children.OfType<Editor>().First().Text = "Edited response";
    service.Mode = "reject";

    Find<Button>(page, "review-dispute-save").SendClicked();

    await WaitUntilAsync(() => model.HasError && !model.IsMutating);
    var error = Find<Label>(page, "review-dispute-action-error");
    Assert.True(error.IsVisible);
    Assert.Equal(model.ErrorMessage, error.Text);
    Assert.Equal(UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsActionFailed), error.Text);
    card.BindingContext = null;
    Assert.Empty(card.Children);
    Assert.Null(error.Text);
    Assert.False(error.IsVisible);
    Lifecycle(page, "OnDisappearing");
  }

  private static ModerationDetailPage<ModerationAppeal> AppealPage(ModerationAppealsViewModel model, string id) =>
      new(token => model.FetchDetailAsync(id, token),
          (detail, token) => new ModerationAppealCardView(model, () => token) { BindingContext = detail });

  private static void Prepare()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    Application.Current ??= new Application();
  }

  private static T Find<T>(Element root, string id) where T : Element =>
      Assert.Single(VisualTreeElements.Descendants<T>(root), element => element.AutomationId == id);

  private static void Lifecycle(Page page, string method) =>
      page.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.Invoke(page, null);

  private static void Loaded(View view) => view.GetType().GetMethod("OnLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!
      .Invoke(view, [null, EventArgs.Empty]);

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(10, TestContext.Current.CancellationToken);
    Assert.True(condition());
  }
}

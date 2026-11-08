using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ModerationDetailPageTests
{
  [Fact]
  public async Task ModalShowsLoadingThenDetail()
  {
    Prepare();
    var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var page = new ModerationDetailPage<string>(_ => response.Task,
        value => new Label { Text = value, AutomationId = "detail-value" });
    Lifecycle(page, "OnAppearing");
    Assert.True(Assert.Single(VisualTreeElements.Descendants<ActivityIndicator>(page)).IsRunning);
    var load = ActiveLoad(page);
    response.SetResult("reporter-1, reporter-2");
    await load;
    Assert.Equal("reporter-1, reporter-2", Assert.Single(VisualTreeElements.Descendants<Label>(page)).Text);
    Lifecycle(page, "OnDisappearing");
  }

  [Fact]
  public async Task DetailFailureShowsErrorWithoutRenderingEditor()
  {
    Prepare();
    var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var page = new ModerationDetailPage<string>(_ => response.Task, _ => new Editor());
    Lifecycle(page, "OnAppearing");
    var load = ActiveLoad(page);
    response.SetException(new HttpRequestException("private server details"));
    await load;
    Assert.Empty(VisualTreeElements.Descendants<Editor>(page));
    Assert.DoesNotContain(VisualTreeElements.Descendants<Label>(page),
        label => label.Text?.Contains("private server details", StringComparison.Ordinal) == true);
    Lifecycle(page, "OnDisappearing");
  }

  [Fact]
  public async Task ClosedDetailIgnoresLateNetworkFailure()
  {
    Prepare();
    CancellationToken requestToken = default;
    var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var page = new ModerationDetailPage<string>(token =>
    {
      requestToken = token;
      return response.Task;
    }, value => new Label { Text = value });
    Lifecycle(page, "OnAppearing");
    var load = ActiveLoad(page);
    Lifecycle(page, "OnDisappearing");
    Assert.True(requestToken.IsCancellationRequested);
    response.SetException(new HttpRequestException("late failure"));
    await load;
    Assert.DoesNotContain(VisualTreeElements.Descendants<Label>(page),
        label => label.AutomationId == "moderation-detail-error");
  }

  [Fact]
  public void FailedDetailCanRetryToLoadTheEditorWithoutReopening()
  {
    Prepare();
    Application.Current!.Resources[UiMessageKey.NativeCommonRetry.Value] = UiLocalization.English.Localize(UiMessageKey.NativeCommonRetry);
    var first = new TaskCompletionSource<string>();
    var second = new TaskCompletionSource<string>();
    var reads = 0;
    var page = new ModerationDetailPage<string>(_ => ++reads == 1 ? first.Task : second.Task,
        draft => new Editor { Text = draft });
    Lifecycle(page, "OnAppearing");
    first.SetException(new HttpRequestException("Temporary failure"));
    var retry = Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.AutomationId == "moderation-detail-retry");
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeCommonRetry), retry.Text);
    Assert.Empty(VisualTreeElements.Descendants<Editor>(page));

    retry.SendClicked();
    retry.SendClicked();

    Assert.Equal(2, reads);
    Assert.True(Assert.Single(VisualTreeElements.Descendants<ActivityIndicator>(page)).IsRunning);
    second.SetResult("Authoritative draft");
    Assert.Equal("Authoritative draft", Assert.Single(VisualTreeElements.Descendants<Editor>(page)).Text);
    Assert.DoesNotContain(VisualTreeElements.Descendants<Label>(page), label => label.AutomationId == "moderation-detail-error");
    Lifecycle(page, "OnDisappearing");
  }

  [Fact]
  public void ClosingRetryCancelsItsReadAndDisablesTheOldRetryAction()
  {
    Prepare();
    var late = new TaskCompletionSource<string>();
    var reads = 0;
    CancellationToken retryToken = default;
    var page = new ModerationDetailPage<string>(token =>
    {
      if (++reads == 1) return Task.FromException<string>(new HttpRequestException("Temporary failure"));
      retryToken = token;
      return late.Task;
    }, draft => new Editor { Text = draft });
    Lifecycle(page, "OnAppearing");
    var retry = Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.AutomationId == "moderation-detail-retry");
    retry.SendClicked();

    Lifecycle(page, "OnDisappearing");
    retry.SendClicked();
    late.SetResult("Late private draft");

    Assert.True(retryToken.IsCancellationRequested);
    Assert.Equal(2, reads);
    Assert.Empty(VisualTreeElements.Descendants<Editor>(page));
  }

  [Fact]
  public void RetryRechecksViewerAuthorityBeforeReadingPrivateDetail()
  {
    Prepare();
    var owner = new NavigationViewer(true, ["administrator"], IdentityId: "staff-1");
    var provider = new MutableNavigationViewerProvider();
    provider.SetViewer(owner);
    var service = new ModerationModalAppealsService { Mode = "reject" };
    using var model = new ModerationAppealsViewModel(service, owner, viewerProvider: provider);
    var page = new ModerationDetailPage<ModerationAppeal>(token => model.FetchDetailAsync(service.Current.Id, token),
        _ => new Editor());
    Lifecycle(page, "OnAppearing");
    var retry = Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.AutomationId == "moderation-detail-retry");
    provider.SetViewer(new NavigationViewer(true, ["administrator"], IdentityId: "staff-2"));
    service.Mode = "normal";

    retry.SendClicked();

    Assert.Equal(1, service.DetailFetches);
    Assert.Empty(VisualTreeElements.Descendants<Editor>(page));
    Assert.Contains(VisualTreeElements.Descendants<Label>(page), label => label.AutomationId == "moderation-detail-error");
    Lifecycle(page, "OnDisappearing");
  }

  private static void Prepare()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    Application.Current ??= new Application();
  }

  private static void Lifecycle(Page page, string method) =>
      page.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!
          .Invoke(page, null);

  private static Task ActiveLoad<T>(ModerationDetailPage<T> page) =>
      (Task)page.GetType().GetField("activeLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
}

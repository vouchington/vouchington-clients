using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
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
    response.SetResult("reporter-1, reporter-2");
    await WaitUntilAsync(() => VisualTreeElements.Descendants<Label>(page)
        .Any(label => label.AutomationId == "detail-value"));
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
    response.SetException(new HttpRequestException("private server details"));
    await WaitUntilAsync(() => VisualTreeElements.Descendants<Label>(page)
        .Any(label => label.AutomationId == "moderation-detail-error"));
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
    Lifecycle(page, "OnDisappearing");
    Assert.True(requestToken.IsCancellationRequested);
    response.SetException(new HttpRequestException("late failure"));
    await response.Task.ContinueWith(_ => { }, TestContext.Current.CancellationToken, TaskContinuationOptions.None, TaskScheduler.Default);
    Assert.DoesNotContain(VisualTreeElements.Descendants<Label>(page),
        label => label.AutomationId == "moderation-detail-error");
  }

  private static void Prepare()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    Application.Current ??= new Application();
  }

  private static void Lifecycle(Page page, string method) =>
      page.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!
          .Invoke(page, null);

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++)
      await Task.Delay(10, TestContext.Current.CancellationToken);
    Assert.True(condition());
  }
}

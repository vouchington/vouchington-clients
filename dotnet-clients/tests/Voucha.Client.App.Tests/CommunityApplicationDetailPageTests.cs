using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Maui.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CommunityApplicationDetailPageTests
{
  [Fact]
  public async Task ModalRendersLoadingFullAnswersAndReadFailure()
  {
    using var handler = new DetailHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
    using var vm = new CommunityApplicationDetailViewModel(new ApiCommunitiesService(new VouchaApiClient(http)), "community", "app");
    var page = new CommunityApplicationDetailPage(vm);
    var loading = vm.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Contains(Labels(page), label => label.AutomationId == "application-detail-loading");
    handler.Response.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent("""
          {"community_application":{"id":"app","community_id":"community","user_id":"applicant",
          "answers":{"why":"A full answer"},"message":"The complete application message",
          "created_at":"2026-01-01T00:00:00Z"}}
          """),
    });
    await loading;
    Assert.Contains(Labels(page), label => label.AutomationId == "application-detail-id" && label.Text == "app");
    Assert.Contains(Labels(page), label => label.AutomationId == "application-detail-message" && label.Text == "The complete application message");
    Assert.Contains(Labels(page), label => label.AutomationId == "application-detail-answer" && label.Text == "A full answer");
    Assert.DoesNotContain(Labels(page), label => label.AutomationId == "application-detail-loading");
    handler.Response = new();
    var failed = vm.LoadAsync(TestContext.Current.CancellationToken);
    handler.Response.SetException(new HttpRequestException("Detail unavailable"));
    await failed;
    Assert.Contains(Labels(page), label => label.AutomationId == "application-detail-error" && label.Text == "Detail unavailable");
    Assert.DoesNotContain(Labels(page), label => label.AutomationId == "application-detail-message");
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task OwnerIdentityChangeRemovesPrivateContentAndRejectsLateReads(bool delayed)
  {
    var service = DispatchProxy.Create<ICommunitiesService, ApplicationService>();
    var scripted = (ApplicationService)service;
    var session = new MutableSession();
    using var parent = new CommunityDetailViewModel(service, session);
    await parent.LoadAsync("community-1", TestContext.Current.CancellationToken);
    using var vm = parent.CreateApplicationDetail("app");
    var page = new CommunityApplicationDetailPage(vm);
    var loading = vm.LoadAsync(TestContext.Current.CancellationToken);
    if (!delayed)
    {
      scripted.Complete();
      await loading;
      Assert.Contains(Labels(page), label => label.AutomationId == "application-detail-message");
    }

    await session.SignOutAsync(TestContext.Current.CancellationToken);
    if (delayed) scripted.Complete();
    await loading;

    Assert.DoesNotContain(Labels(page), label => label.AutomationId is "application-detail-message" or "application-detail-answer");
    Assert.DoesNotContain(Labels(page), label => label.Text == "Private message");
  }

  [Fact]
  public async Task MismatchedResponseRendersAnErrorWithoutTheOtherApplication()
  {
    var service = DispatchProxy.Create<ICommunitiesService, ApplicationService>();
    ((ApplicationService)service).Complete();
    using var vm = new CommunityApplicationDetailViewModel(service, "community-1", "another-app");
    var page = new CommunityApplicationDetailPage(vm);

    await vm.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Contains(Labels(page), label => label.AutomationId == "application-detail-error");
    Assert.DoesNotContain(Labels(page), label => label.AutomationId == "application-detail-message");
  }

  public class ApplicationService : CommunityModerationPageAutomodNavigationTests.CommunityServiceProxy
  {
    private readonly TaskCompletionSource<CommunityApplicationResponse> detail = new();
    public void Complete() => detail.SetResult(JsonSerializer.Deserialize<CommunityApplicationResponse>("""
        {"community_application":{"id":"app","community_id":"community-1","user_id":"applicant",
        "answers":{"why":"Private answer"},"message":"Private message","created_at":"2026-01-01T00:00:00Z"}}
        """, VouchaApiJson.Options)!);
    protected override object? Invoke(MethodInfo? method, object?[]? args) =>
        method?.Name == nameof(ICommunitiesService.FetchApplicationAsync) ? detail.Task : base.Invoke(method, args);
  }

  private sealed class MutableSession : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; private set; } = new(new User("moderator-1", "moderator", Roles: []));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
      Current = SessionSnapshot.Anonymous;
      SessionChanged?.Invoke(this, new(Current));
      return Task.CompletedTask;
    }
  }

  private static IEnumerable<Label> Labels(ContentPage page) => page.GetVisualTreeDescendants().OfType<Label>();

  private sealed class DetailHandler : HttpMessageHandler
  {
    public TaskCompletionSource<HttpResponseMessage> Response { get; set; } = new();
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Response.Task;
  }
}

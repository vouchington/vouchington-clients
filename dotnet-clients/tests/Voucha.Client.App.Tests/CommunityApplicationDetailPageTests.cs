using System.Net;
using Microsoft.Maui.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
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
    var vm = new CommunityApplicationDetailViewModel(new ApiCommunitiesService(new VouchaApiClient(http)), "community", "app");
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

  private static IEnumerable<Label> Labels(ContentPage page) => page.GetVisualTreeDescendants().OfType<Label>();

  private sealed class DetailHandler : HttpMessageHandler
  {
    public TaskCompletionSource<HttpResponseMessage> Response { get; set; } = new();
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Response.Task;
  }
}

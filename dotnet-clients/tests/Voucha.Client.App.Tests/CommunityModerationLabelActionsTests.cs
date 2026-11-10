using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CommunityModerationLabelActionsTests
{
  [Fact]
  public async Task LoadedReportLabelsPopulatePickerAndSelectionDrivesOpaqueActionIds()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    UiCopy.UseLocalization(UiLocalization.English);
    _ = new Application();
    foreach (var key in UiMessageKey.All)
      Application.Current!.Resources[key.Value] = UiLocalization.English.Localize(key);
    var handler = new FixtureHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var session = new Session();
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(new VouchaApiClient(http)), session);
    var page = new CommunityModerationPage(model, session, new ServiceCollection().BuildServiceProvider());
    await model.LoadSurfaceAsync("test-community", CommunityDetailSurfaceSection.Moderation,
        TestContext.Current.CancellationToken);
    var picker = Assert.Single(VisualTreeElements.Descendants<Picker>(page),
        element => element.AutomationId == "community-moderation-target");
    var report = JsonSerializer.Deserialize<CommunityPendingReportsResponse>(
        Fixture("native.community.pending-reports.paginated"), VouchaApiJson.Options)!.Reports[0];
    var selected = Assert.Single(picker.ItemsSource.Cast<CommunityModerationRow>(), row => row.Id == report.Id);
    Assert.Equal(report.TargetLabel, selected.Title);
    Assert.Equal(nameof(CommunityModerationRow.Title), Assert.IsType<Binding>(picker.ItemDisplayBinding).Path);
    Assert.DoesNotContain(VisualTreeElements.Descendants<Entry>(page), entry =>
        entry.Placeholder == UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpCommunitiesPostId) ||
        entry.Placeholder == UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpCommunitiesUserId));
    picker.SelectedItem = selected;
    var resolve = Assert.Single(VisualTreeElements.Descendants<Button>(page), button =>
        button.Text == UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpCommunitiesResolveReport));
    Assert.True(resolve.IsEnabled);
    resolve.SendClicked();
    await PageTestWait.WaitUntilAsync(() => handler.MutatedPath is not null);
    Assert.Equal($"/api/v1/communities/test-community/reports/{report.Id}", handler.MutatedPath);
    Assert.Equal("reviewed", JsonDocument.Parse(handler.MutatedBody!).RootElement.GetProperty("status").GetString());
  }

  private static string Fixture(string id)
  {
    using var manifest = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("manifest.json")));
    var file = manifest.RootElement.GetProperty("fixtures").EnumerateArray()
        .Single(entry => entry.GetProperty("id").GetString() == id).GetProperty("bodyFile").GetString()!;
    return File.ReadAllText(FilamentsContractPaths.ApiFixture(file));
  }

  private sealed class Session : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User("moderator-1", "moderator", Roles: ["administrator"]));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class FixtureHandler : HttpMessageHandler
  {
    public string? MutatedPath { get; private set; }
    public string? MutatedBody { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri!.AbsolutePath;
      if (request.Method == HttpMethod.Patch)
      {
        MutatedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
        MutatedPath = path;
        return new HttpResponseMessage(HttpStatusCode.NoContent);
      }
      var fixture = path.Split('/').Last() switch
      {
        "test-community" => "web.communities.show.default",
        "members" => "web.communities.members.default",
        "posts" => "web.communities.posts.default",
        "counts" => "web.communities.list-items.counts.default",
        "moderator-stats" => "web.communities.moderator-stats.default",
        "moderation-queue" => "web.communities.moderation-queue.default",
        "pending" => "native.community.pending-reports.paginated",
        _ => throw new InvalidOperationException($"Unexpected request {path}"),
      };
      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(Fixture(fixture), Encoding.UTF8, "application/json"),
        RequestMessage = request,
      };
    }
  }
}

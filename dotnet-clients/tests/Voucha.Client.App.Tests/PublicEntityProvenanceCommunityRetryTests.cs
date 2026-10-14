using System.ComponentModel;
using System.Text.Json.Nodes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class PublicEntityProvenanceRenderingTests
{
  [Fact]
  public async Task EditableCommunityRetriesFailedStaffReloadOnNextAppearance()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    var publicDetail = Fixture("web.communities.show.default");
    var staffDetail = JsonNode.Parse(publicDetail)!;
    staffDetail["community"]!["staff_provenance"] = JsonNode.Parse("""
        {"created_via":"mcp","oauth_client":null}
        """);
    var members = Fixture("web.communities.members.default");
    var posts = Fixture("web.communities.posts.default");
    var counts = Fixture("web.communities.list-items.counts.default");
    var session = new MutableStaffSessionStore();
    session.SetCurrent(SessionSnapshot.Anonymous);
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(Client(
        publicDetail, members, posts, counts, "not-json", staffDetail.ToJsonString(), members, posts, counts)), session);
    var page = new CommunityDetailPage(model, session);
    await model.LoadAsync("test-community", TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Loaded, model.State);
    Assert.Null(page.Slug);
    Assert.Single(Descendants<Entry>(page)).Text = "test-community";
    InvokeLifecycle(page, "OnAppearing");
    InvokeLifecycle(page, "OnDisappearing");

    session.SetCurrent(new SessionSnapshot(new User("staff", "staff", Roles: ["moderator"])));
    var failed = WaitForCommunityStateAsync(model, LoadState.Error);
    InvokeLifecycle(page, "OnAppearing");
    await failed;
    Assert.True(model.HasError);
    InvokeLifecycle(page, "OnDisappearing");

    var recovered = WaitForCommunityStateAsync(model, LoadState.Loaded);
    InvokeLifecycle(page, "OnAppearing");
    await recovered;
    Assert.Equal("mcp", model.Community?.StaffProvenance?.CreatedVia);
  }

  private static async Task WaitForCommunityStateAsync(CommunityDetailViewModel model, LoadState expected)
  {
    var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    void OnChanged(object? sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName == nameof(model.State) && model.State == expected) reached.TrySetResult();
    }
    model.PropertyChanged += OnChanged;
    try
    {
      await reached.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
    finally
    {
      model.PropertyChanged -= OnChanged;
    }
  }
}

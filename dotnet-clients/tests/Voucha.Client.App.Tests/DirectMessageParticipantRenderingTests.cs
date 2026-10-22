using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Messages;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class DirectMessageParticipantRenderingTests
{
  [Fact]
  public void RosterPreservesIdentityAndRoleWhilePlacementClears()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    app.Resources["Body"] = new Style(typeof(Label));
    app.Resources["Metadata"] = new Style(typeof(Label));
    app.Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")), 96);
    var view = new DirectMessageParticipantView();
    var row = new DirectMessageParticipantRow("participant", "alice", UiText.Verbatim("@alice"),
        "member", UiText.Verbatim("Member"), true, UiText.Verbatim("Remove"), UiLocalization.English,
        new TopicImagePlacement("avatar-1", "participant-placement", 2));

    view.BindingContext = row;

    var image = Assert.Single(view.GetVisualTreeDescendants().OfType<Image>());
    Assert.False(AutomationProperties.GetIsInAccessibleTree(image));
    var grid = Assert.Single(view.GetVisualTreeDescendants().OfType<Grid>());
    Assert.Equal(new Uri("https://images.test/images/placements/participant-placement/2/avatar-1?w=96"),
        Assert.IsType<UriImageSource>(image.Source).Uri);
    Assert.True(image.IsVisible);
    Assert.Equal(new GridLength(44), grid.ColumnDefinitions[0].Width);
    Assert.Contains(view.GetVisualTreeDescendants().OfType<Label>(), label => label.Text == "@alice");
    Assert.Contains(view.GetVisualTreeDescendants().OfType<Label>(), label => label.Text == "Member");

    view.BindingContext = row with { ProfileImagePlacement = null };

    Assert.Null(image.Source);
    Assert.False(image.IsVisible);
    Assert.Equal(new GridLength(0), grid.ColumnDefinitions[0].Width);
    Assert.Contains(view.GetVisualTreeDescendants().OfType<Label>(), label => label.Text == "@alice");
    Assert.Contains(view.GetVisualTreeDescendants().OfType<Label>(), label => label.Text == "Member");

    view.BindingContext = row with
    {
      ProfileImagePlacement = new TopicImagePlacement("avatar-1", "", 2),
    };

    Assert.Null(image.Source);
    Assert.False(image.IsVisible);
    Assert.Equal(new GridLength(0), grid.ColumnDefinitions[0].Width);
    Assert.Contains(view.GetVisualTreeDescendants().OfType<Label>(), label => label.Text == "@alice");
  }

  [Fact]
  public async Task RosterHidesPlacementForMissingWithheldDeletedAndAnonymousParticipants()
  {
    InstallResources();
    var fixture = JsonSerializer.Deserialize<DirectMessageParticipantsResponse>(
        File.ReadAllText(FilamentsContractPaths.ApiFixture(BodyFile("native.messages.participants.default"))),
        VouchaApiJson.Options)!;
    var owner = fixture.Results[0];
    var member = fixture.Results[1];
    const string legacyImageId = "00000000-0000-7000-8000-000000000211";

    var missing = await LoadRoster(fixture with { Results = [owner] });
    AssertHiddenAvatar(Assert.Single(missing), "@fixtureuser", "Owner");
    Assert.Null(owner.ProfileImageId);
    Assert.Null(owner.ProfileImagePlacement);

    var withheldParticipant = member with { ProfileImagePlacement = null };
    Assert.Equal(legacyImageId, withheldParticipant.ProfileImageId);
    var withheld = await LoadRoster(fixture with { Results = [owner, withheldParticipant] });
    AssertHiddenAvatar(withheld[1], "@alice", "Member");
    Assert.True(withheld[1].CanRemove);

    var deletedParticipant = member with
    {
      Username = null,
      ProfileImageId = null,
      ProfileImagePlacement = null,
    };
    var deleted = await LoadRoster(fixture with { Results = [owner, deletedParticipant] });
    AssertHiddenAvatar(deleted[1], "Member", "Member");
    Assert.Equal(member.UserId, deleted[1].UserId);
    Assert.True(deleted[1].CanRemove);

    var anonymousParticipant = deletedParticipant with { UserId = null };
    var anonymous = await LoadRoster(fixture with { Results = [owner, anonymousParticipant] });
    AssertHiddenAvatar(anonymous[1], "Member", "Member");
    Assert.Null(anonymous[1].UserId);
    Assert.False(anonymous[1].CanRemove);
  }

  private static async Task<IReadOnlyList<DirectMessageParticipantRow>> LoadRoster(
      DirectMessageParticipantsResponse participants)
  {
    var service = DispatchProxy.Create<IDirectMessagesService, RosterService>();
    var roster = (RosterService)(object)service;
    roster.Participants = participants;
    using var model = new DirectMessagesViewModel(
        service,
        "00000000-0000-7000-8000-000000000001",
        UiLocalization.English);
    await model.SelectConversationAsync(
        "00000000-0000-7000-8000-000000000101",
        TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Loaded, model.ThreadState);
    return model.Participants;
  }

  private static void AssertHiddenAvatar(DirectMessageParticipantRow row, string label, string role)
  {
    var view = new DirectMessageParticipantView { BindingContext = row };
    var image = Assert.Single(view.GetVisualTreeDescendants().OfType<Image>());
    var grid = Assert.Single(view.GetVisualTreeDescendants().OfType<Grid>());
    Assert.Null(image.Source);
    Assert.False(image.IsVisible);
    Assert.False(AutomationProperties.GetIsInAccessibleTree(image));
    Assert.Equal(new GridLength(0), grid.ColumnDefinitions[0].Width);
    Assert.Contains(view.GetVisualTreeDescendants().OfType<Label>(), item => item.Text == label);
    Assert.Contains(view.GetVisualTreeDescendants().OfType<Label>(), item => item.Text == role);
  }

  private static void InstallResources()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    app.Resources["Body"] = new Style(typeof(Label));
    app.Resources["Metadata"] = new Style(typeof(Label));
    app.Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")), 96);
  }

  private static string BodyFile(string id)
  {
    var manifest = JsonNode.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("manifest.json")))!;
    return manifest["fixtures"]!.AsArray().Single(entry => (string?)entry?["id"] == id)!["bodyFile"]!.GetValue<string>();
  }

  public class RosterService : DispatchProxy
  {
    public DirectMessageParticipantsResponse Participants { get; set; } =
        new([], new PageInfo(null, false, null));

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod?.Name switch
        {
          nameof(IDirectMessagesService.FetchDirectConversationAsync) => Task.FromResult(
              new DirectConversationResponse(new DirectConversation(
                  "00000000-0000-7000-8000-000000000101",
                  "direct_message",
                  "Direct",
                  DateTimeOffset.UnixEpoch,
                  DateTimeOffset.UnixEpoch))),
          nameof(IDirectMessagesService.FetchDirectConversationMessagesAsync) => Task.FromResult(
              new DirectMessagesResponse([], new PageInfo(null, false, null))),
          nameof(IDirectMessagesService.FetchDirectConversationParticipantsAsync) => Task.FromResult(Participants),
          _ => targetMethod?.ReturnType == typeof(Task) ? Task.CompletedTask : null,
        };
  }
}

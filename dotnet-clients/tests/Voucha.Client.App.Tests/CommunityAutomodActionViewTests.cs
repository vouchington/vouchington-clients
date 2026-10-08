using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CommunityAutomodActionViewTests
{
  [Theory]
  [InlineData("record_only")]
  [InlineData("review_queue")]
  [InlineData("unpublish")]
  public void SelectsTheLocalizedCommunityLevelAction(string action)
  {
    ConfigureResources();
    var view = new CommunityAutomodActionView(action);

    Assert.True(view.IsVisible);
    Assert.Contains(view.Children.OfType<Label>(), label => label.Text == "Automod");
    Assert.Equal(action, view.SelectedAction);
    var descriptionKey = action switch
    {
      "record_only" => UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormKeepThePostPublishedTheFlagAppears01b0cba0,
      "review_queue" => UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormKeepThePostPublishedAndAddIt56f75b30,
      _ => UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormRemoveThePostFromTheCommunityRight215c4ec5,
    };
    Assert.Equal(UiCopy.Localize(descriptionKey), Assert.Single(view.Children.OfType<Label>(),
        label => label.AutomationId == "community-automod-action-description").Text);
    Assert.Equal(["Record only", "Send to review queue", "Unpublish"],
        Assert.Single(view.Children.OfType<Picker>()).ItemsSource.Cast<string>());
  }

  [Fact]
  public void SavesSelectedActionThroughTheActualButtonAndReconcilesCommittedValue()
  {
    ConfigureResources();
    string? submitted = null;
    var view = new CommunityAutomodActionView("record_only", action =>
    {
      submitted = action;
      return Task.FromResult(true);
    });
    var picker = Assert.Single(view.Children.OfType<Picker>());
    picker.SelectedIndex = 2;
    Assert.Equal("unpublish", view.SelectedAction);

    Assert.Single(view.Children.OfType<Button>()).SendClicked();

    Assert.Equal("unpublish", submitted);
    Assert.Equal(UiCopy.Localize(UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormAutomodActionSaved5a7127b1), Assert.Single(view.Children.OfType<Label>(), label => label.AutomationId == "community-automod-action-status").Text);
    view.Update("unpublish");
    Assert.Equal("unpublish", view.SelectedAction);
    view.Update(null);
    Assert.False(view.IsVisible);
  }

  [Fact]
  public void FailedSaveRestoresCommittedSelection()
  {
    ConfigureResources();
    var view = new CommunityAutomodActionView("record_only", _ => Task.FromResult(false));
    var picker = Assert.Single(view.Children.OfType<Picker>());
    picker.SelectedIndex = 2;

    Assert.Single(view.Children.OfType<Button>()).SendClicked();

    Assert.Equal("record_only", view.SelectedAction);
    Assert.Equal(UiCopy.Localize(UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormCouldNotSaveTheAutomodActionD095dea5), Assert.Single(view.Children.OfType<Label>(), label => label.AutomationId == "community-automod-action-status").Text);
  }

  private static void ConfigureResources()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources =
    {
      [UiMessageKey.NativeDotnetModerationAutomod.Value] = "Automod",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormAutomodAction83211784.Value] = "Automod",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormChooseWhatHappensToApublishedPostB2e29694.Value] = "Choose what happens",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormRecordOnlyC9184072.Value] = "Record only",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormSendToReviewQueue1c3a1b74.Value] = "Send to review queue",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormUnpublish2db04a54.Value] = "Unpublish",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormSaveAutomodAction5f2b209e.Value] = "Save action",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormKeepThePostPublishedAndAddIt56f75b30.Value] = "Keep published, review queue",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormKeepThePostPublishedTheFlagAppears01b0cba0.Value] = "Keep published, add flag",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormRemoveThePostFromTheCommunityRight215c4ec5.Value] = "Remove post",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormSavingDc85af8f.Value] = "Saving",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormAutomodActionSaved5a7127b1.Value] = "Saved",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormCouldNotSaveTheAutomodActionD095dea5.Value] = "Could not save",
    } };
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}

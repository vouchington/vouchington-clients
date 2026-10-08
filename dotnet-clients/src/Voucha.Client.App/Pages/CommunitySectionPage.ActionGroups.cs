using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private View BuildSettingsActions()
  {
    var title = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesSavedReplyTitle);
    var body = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesSavedReplyBody);
    settingsAllowReviewPostsSwitch = new Switch();
    settingsAllowDataPointPostsSwitch = new Switch();
    settingsAutomodActionView = new CommunityAutomodActionView(viewModel.Community?.AutomodAction);
    UpdateSettingsSwitches();

    return new VerticalStackLayout
    {
      Spacing = 8,
      Children =
      {
        settingsAutomodActionView,
        UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesSavedReplies),
        title,
        body,
        ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesCreateReply).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.CreateSavedReplyAsync(title.Text ?? string.Empty, body.Text ?? string.Empty, ct)).ConfigureAwait(true)),
        ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesRefreshReplies).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(viewModel.LoadSavedRepliesAsync).ConfigureAwait(true)),
        UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesPostTypeSettings),
        new HorizontalStackLayout { Spacing = 8, Children = { UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesReviewPosts), settingsAllowReviewPostsSwitch!, UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesDataPointPosts), settingsAllowDataPointPostsSwitch! } },
        ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesUpdateSettings).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.UpdatePostTypeSettingsAsync(settingsAllowReviewPostsSwitch!.IsToggled, settingsAllowDataPointPostsSwitch!.IsToggled, ct)).ConfigureAwait(true)),
      },
    };
  }

  private View BuildListsActions()
  {
    var type = ListItemTypePicker();
    var id = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesItemId);
    return new HorizontalStackLayout
    {
      Spacing = 8,
      Children =
      {
        type,
        id,
        ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesAdd).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.AddListItemAsync(ListItemRequest(type, id.Text), ct)).ConfigureAwait(true)),
        ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesRemove).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.RemoveListItemAsync(SelectedListItemType(type).RouteSegment, id.Text ?? string.Empty, ct)).ConfigureAwait(true)),
      },
    };
  }

  private View BuildApplicationsActions()
  {
    var applicationId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesApplicationId);
    var status = TextField(UiMessageKey.NativeDotnetResidualStatus, UiText.Verbatim("approved"));
    var reason = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesReason);
    return new HorizontalStackLayout
    {
      Spacing = 8,
      Children =
      {
        applicationId,
        status,
        reason,
        ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesReview).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.ReviewApplicationAsync(applicationId.Text ?? string.Empty, status.Text ?? "approved", reason.Text, ct)).ConfigureAwait(true)),
      },
    };
  }

  private View BuildInvitesActions()
  {
    var inviteId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesInviteId);
    return new HorizontalStackLayout
    {
      Spacing = 8,
      Children =
      {
        inviteId,
        ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesRevoke).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.RevokeInviteAsync(inviteId.Text ?? string.Empty, ct)).ConfigureAwait(true)),
      },
    };
  }

  private View BuildPinnedPostsActions()
  {
    var postIds = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesPostIds);
    return new HorizontalStackLayout
    {
      Spacing = 8,
      Children =
      {
        postIds,
        ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesUpdate).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.UpdatePinnedPostsAsync((postIds.Text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), ct)).ConfigureAwait(true)),
      },
    };
  }

  private View BuildBansActions()
  {
    var userId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesUserId);
    var reason = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesReason);
    var expiresAt = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesExpiresAt);
    return new VerticalStackLayout
    {
      Spacing = 8,
      Children =
      {
        new HorizontalStackLayout { Spacing = 8, Children = { userId, reason, expiresAt } },
        new HorizontalStackLayout
        {
          Spacing = 8,
          Children =
          {
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesBan).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct =>
            {
              DateTimeOffset? parsed = DateTimeOffset.TryParse(expiresAt.Text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value) ? value : null;
              return viewModel.BanAsync(userId.Text ?? string.Empty, OptionalText(reason), parsed, ct);
            }).ConfigureAwait(true)),
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesLift).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.LiftBanAsync(userId.Text ?? string.Empty, ct)).ConfigureAwait(true)),
          },
        },
      },
    };
  }

  private View BuildRestrictionsActions()
  {
    var restrictionTypes = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesRestrictionTypes, UiText.Verbatim("require_post_approval"));
    var reason = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesReason);
    var expiresAt = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesExpiresAt);
    var restrictionId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesRestrictionId);
    return new VerticalStackLayout
    {
      Spacing = 8,
      Children =
      {
        new HorizontalStackLayout { Spacing = 8, Children = { restrictionTypes, reason, expiresAt, restrictionId } },
        new HorizontalStackLayout
        {
          Spacing = 8,
          Children =
          {
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesActivate).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct =>
            {
              DateTimeOffset? parsed = DateTimeOffset.TryParse(expiresAt.Text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value) ? value : null;
              return viewModel.ActivateRestrictionsAsync((restrictionTypes.Text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), parsed, OptionalText(reason), ct);
            }).ConfigureAwait(true)),
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesLift).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.LiftRestrictionAsync(restrictionId.Text ?? string.Empty, ct)).ConfigureAwait(true)),
          },
        },
      },
    };
  }

}

static class CommunitySectionPageButtonExtensions
{
  public static T Also<T>(this T value, Action<T> action)
  {
    action(value);
    return value;
  }
}

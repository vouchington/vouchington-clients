using System.Globalization;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private View BuildVacationActions()
  {
    var endsAt = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesEndsAt);
    var suppressDigests = new Switch
    {
      AutomationId = "suppress-community-digests-while-on-vacation",
    };
    suppressDigests.SetBinding(
        Switch.IsToggledProperty,
        new Binding(nameof(CommunityDetailViewModel.SuppressCommunityDigestsWhileOnVacation), mode: BindingMode.OneWay));
    suppressDigests.Toggled += async (_, args) =>
    {
      if (args.Value == viewModel.SuppressCommunityDigestsWhileOnVacation)
      {
        return;
      }
      try
      {
        await InvokeAndRenderAsync(ct => viewModel.SetSuppressCommunityDigestsWhileOnVacationAsync(args.Value, ct)).ConfigureAwait(true);
      }
      finally
      {
        if (suppressDigests.IsToggled != viewModel.SuppressCommunityDigestsWhileOnVacation)
        {
          suppressDigests.IsToggled = viewModel.SuppressCommunityDigestsWhileOnVacation;
        }
      }
    };
    return new VerticalStackLayout
    {
      Spacing = 8,
      Children =
      {
        new HorizontalStackLayout
        {
          Spacing = 8,
          Children =
          {
            endsAt,
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesSet).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct =>
            {
              DateTimeOffset? parsed = DateTimeOffset.TryParse(endsAt.Text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value) ? value : null;
              return viewModel.SetModeratorVacationAsync(parsed, ct);
            }).ConfigureAwait(true)),
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesClear).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(viewModel.ClearModeratorVacationAsync).ConfigureAwait(true)),
          },
        },
        new HorizontalStackLayout
        {
          Spacing = 8,
          Children = { UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesPauseDigests), suppressDigests },
        },
      },
    };
  }
}

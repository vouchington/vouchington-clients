using Microsoft.Maui.Controls.Shapes;
using Voucha.Client.Core.Tags;
using Voucha.Client.Core.Api;

namespace Voucha.Client.App.Pages;

public sealed partial class TagManagementPage
{
  private DataTemplate BuildSearchResultTemplate() =>
      new(() =>
      {
        var title = new Label { FontAttributes = FontAttributes.Bold };
        title.SetBinding(Label.TextProperty, nameof(TagSearchResultRow.Title));
        var subtitle = new Label { FontSize = 12, TextColor = Colors.Gray };
        subtitle.SetBinding(Label.TextProperty, nameof(TagSearchResultRow.Subtitle));
        var addButton = UiCopy.Bind(
            new Button(),
            Button.TextProperty,
            Voucha.Client.Core.Localization.UiMessageKey.NativeDotnetCsharpAdd);
        addButton.SetBinding(Button.CommandParameterProperty, new Binding("."));
        addButton.Clicked += async (sender, _) =>
        {
          if (sender is Button { CommandParameter: TagSearchResultRow row })
          {
            await AddAsync(row.Id).ConfigureAwait(true);
          }
        };

        var info = new VerticalStackLayout { Children = { title, subtitle } };
        Grid.SetColumn(info, 0);
        Grid.SetColumn(addButton, 1);
        return new Border
        {
          Stroke = Color.FromArgb("#D8DEE9"),
          StrokeThickness = 1,
          StrokeShape = new RoundRectangle { CornerRadius = 8 },
          Padding = 12,
          Content = new Grid
          {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Children = { info, addButton },
          },
        };
      });

  private DataTemplate BuildRelationTemplate() =>
      new(() =>
      {
        var title = new Label { FontAttributes = FontAttributes.Bold };
        title.SetBinding(Label.TextProperty, nameof(TagRelationRow.Title));
        var subtitle = new Label { FontSize = 12, TextColor = Colors.Gray };
        subtitle.SetBinding(Label.TextProperty, nameof(TagRelationRow.Subtitle));
        var myVote = new Label { FontSize = 12, TextColor = Colors.Gray };
        myVote.SetBinding(Label.TextProperty, new Binding(nameof(TagRelationRow.MyVote)));
        var confirm = VoteButton(ElectionVoteChoice.Confirm);
        var dispute = VoteButton(ElectionVoteChoice.Dispute);
        var clear = ClearVoteButton();
        return new Border
        {
          Stroke = Color.FromArgb("#D8DEE9"),
          StrokeThickness = 1,
          StrokeShape = new RoundRectangle { CornerRadius = 8 },
          Padding = 12,
          Content = new VerticalStackLayout
          {
            Spacing = 8,
            Children = { title, subtitle, myVote, new HorizontalStackLayout { Spacing = 8, Children = { confirm, dispute, clear } } },
          },
        };
      });

  private Button VoteButton(ElectionVoteChoice choice)
  {
    var button = new Button { Text = Voucha.Client.App.Controls.SemanticVoteActionSheet.Label(choice), MinimumHeightRequest = 44 };
    button.AutomationId = $"tag-relation-{choice.ToString().ToLowerInvariant()}";
    button.Opacity = 0.65;
    button.SetBinding(Button.CommandParameterProperty, new Binding("."));
    button.SetBinding(IsEnabledProperty, TagRelationVoteEligibilityBinding(choice));
    button.Triggers.Add(new DataTrigger(typeof(Button))
    {
      Binding = new Binding(nameof(TagRelationRow.MyVote)),
      Value = choice,
      Setters = { new Setter { Property = VisualElement.OpacityProperty, Value = 1.0 } },
    });
    button.Clicked += async (sender, _) =>
    {
      if (sender is Button { IsEnabled: true, CommandParameter: TagRelationRow row })
      {
        await VoteAsync(row, choice).ConfigureAwait(true);
      }
    };
    return button;
  }

  private Button ClearVoteButton()
  {
    var button = new Button
    {
      Text = Voucha.Client.App.Controls.SemanticVoteActionSheet.ClearLabel,
      MinimumHeightRequest = 44,
      AutomationId = "tag-relation-clear",
    };
    button.SetBinding(Button.CommandParameterProperty, new Binding("."));
    button.SetBinding(IsEnabledProperty, TagRelationVoteEligibilityBinding(null));
    button.Clicked += async (sender, _) =>
    {
      if (sender is Button { IsEnabled: true, CommandParameter: TagRelationRow row })
      {
        await VoteAsync(row, null).ConfigureAwait(true);
      }
    };
    return button;
  }

  private MultiBinding TagRelationVoteEligibilityBinding(ElectionVoteChoice? targetChoice) =>
      new()
      {
        Converter = new TagRelationVoteEligibilityConverter(
            sessionStore,
            () => viewModel.Context?.EntityType == "user",
            targetChoice),
        Bindings =
        {
          new Binding(nameof(TagRelationRow.MyVote)),
          new Binding(nameof(TagManagementViewModel.IsLoading), source: viewModel),
          new Binding(nameof(VotePolicyVersion), source: this),
        },
      };
}

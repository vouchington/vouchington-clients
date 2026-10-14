using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

internal sealed partial class FollowerDistributionSendPage
{
  private readonly Dictionary<string, RecipientRow> recipientRows = new(StringComparer.Ordinal);
  private readonly Dictionary<string, RecipientRow> selectedRecipientRows = new(StringComparer.Ordinal);

  private void RefreshRows(
      VerticalStackLayout container,
      IReadOnlyList<User> users,
      Dictionary<string, RecipientRow> rows,
      bool isSelected)
  {
    var ids = users.Select(user => user.Id).ToHashSet(StringComparer.Ordinal);
    foreach (var removedId in rows.Keys.Where(id => !ids.Contains(id)).ToArray())
    {
      container.Children.Remove(rows[removedId].Layout);
      rows.Remove(removedId);
    }

    foreach (var user in users)
    {
      if (!rows.TryGetValue(user.Id, out var row))
      {
        row = CreateRow(user, isSelected);
        rows.Add(user.Id, row);
      }
      UpdateRow(row, user);
    }

    for (var index = 0; index < users.Count; index++)
    {
      var layout = rows[users[index].Id].Layout;
      if (index < container.Children.Count && ReferenceEquals(container.Children[index], layout)) continue;
      var oldIndex = container.Children.IndexOf(layout);
      if (oldIndex >= 0) container.Children.RemoveAt(oldIndex);
      container.Children.Insert(index, layout);
    }
  }

  private RecipientRow CreateRow(User user, bool isSelected)
  {
    var avatar = CreateAvatar(user);
    var label = new Label { Text = Name(user), VerticalOptions = LayoutOptions.Center };
    var layout = new HorizontalStackLayout { Spacing = 8 };
    CheckBox? checkbox = null;
    if (isSelected)
    {
      var remove = new Button { Text = T(UiMessageKey.NativeSwiftCommonRemove) };
      remove.Clicked += (_, _) => { state.ToggleSelection(user.Id); Refresh(); };
      layout.Children.Add(avatar.View);
      layout.Children.Add(label);
      layout.Children.Add(remove);
    }
    else
    {
      checkbox = new CheckBox { AutomationId = $"follower-recipient-checkbox-{user.Id}" };
      var row = new RecipientRow(user, layout, label, avatar, checkbox);
      checkbox.CheckedChanged += (_, _) =>
      {
        if (row.IsUpdating) return;
        var changed = state.ToggleSelection(row.User);
        Refresh();
        if (!changed) error.Text = T(UiMessageKey.NativeSwiftFollowerDistributionUpToOneHundredFollowers);
      };
      layout.Children.Add(checkbox);
      layout.Children.Add(avatar.View);
      layout.Children.Add(label);
      return row;
    }
    return new RecipientRow(user, layout, label, avatar, checkbox);
  }

  private void UpdateRow(RecipientRow row, User user)
  {
    row.User = user;
    row.NameLabel.Text = Name(user);
    UpdateAvatar(row.Avatar, user);
    if (row.CheckBox is null) return;
    row.IsUpdating = true;
    row.CheckBox.IsChecked = state.SelectedIds.Contains(user.Id);
    row.IsUpdating = false;
  }

  private AvatarParts CreateAvatar(User user)
  {
    var name = Name(user);
    var placement = user.ProfileImagePlacement;
    var imageUrl = placement is null
        ? null
        : config.ImageUrlForPlacement(placement.PlacementId, placement.PlacementRevision, placement.ImageId, 96);
    var initial = name.Trim().Length == 0 ? "?" : StringInfo.GetNextTextElement(name.Trim()).ToUpperInvariant();
    var initialLabel = new Label
    {
      Text = initial,
      HorizontalOptions = LayoutOptions.Center,
      VerticalOptions = LayoutOptions.Center,
      AutomationId = "follower-recipient-avatar-initial"
    };
    AutomationProperties.SetIsInAccessibleTree(initialLabel, false);
    var image = new Image
    {
      WidthRequest = 40,
      HeightRequest = 40,
      Aspect = Aspect.AspectFill,
      Source = imageUrl is null ? null : ImageSource.FromUri(imageUrl),
      AutomationId = "follower-recipient-avatar-image",
      Clip = new EllipseGeometry { Center = new Point(20, 20), RadiusX = 20, RadiusY = 20 }
    };
    AutomationProperties.SetIsInAccessibleTree(image, false);
    var view = new Border
    {
      WidthRequest = 40,
      HeightRequest = 40,
      StrokeThickness = 0,
      BackgroundColor = Color.FromArgb("#E0F2FE"),
      StrokeShape = new RoundRectangle { CornerRadius = 20 },
      Content = new Grid { Children = { initialLabel, image } }
    };
    return new AvatarParts(view, image, initialLabel);
  }

  private void UpdateAvatar(AvatarParts avatar, User user)
  {
    var name = Name(user);
    var placement = user.ProfileImagePlacement;
    var imageUrl = placement is null
        ? null
        : config.ImageUrlForPlacement(placement.PlacementId, placement.PlacementRevision, placement.ImageId, 96);
    var currentUrl = (avatar.Image.Source as UriImageSource)?.Uri;
    var nextUrl = imageUrl;
    if (currentUrl?.AbsoluteUri != nextUrl?.AbsoluteUri)
      avatar.Image.Source = nextUrl is null ? null : ImageSource.FromUri(nextUrl);
    var initial = name.Trim().Length == 0 ? "?" : StringInfo.GetNextTextElement(name.Trim()).ToUpperInvariant();
    if (avatar.Initial.Text != initial) avatar.Initial.Text = initial;
  }

  private sealed class RecipientRow(
      User user,
      HorizontalStackLayout layout,
      Label nameLabel,
      AvatarParts avatar,
      CheckBox? checkBox)
  {
    public User User { get; set; } = user;
    public HorizontalStackLayout Layout { get; } = layout;
    public Label NameLabel { get; } = nameLabel;
    public AvatarParts Avatar { get; } = avatar;
    public CheckBox? CheckBox { get; } = checkBox;
    public bool IsUpdating { get; set; }
  }

  private sealed record AvatarParts(View View, Image Image, Label Initial);
}

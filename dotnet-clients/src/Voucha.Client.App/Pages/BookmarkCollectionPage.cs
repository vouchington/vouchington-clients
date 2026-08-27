using System.Diagnostics.CodeAnalysis;
using Microsoft.Maui.Controls.Shapes;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class BookmarkCollectionPage : ContentPage
{
  private readonly BookmarkCollectionViewModel viewModel;
  private readonly IServiceProvider serviceProvider;

  public BookmarkCollectionPage(BookmarkCollectionViewModel viewModel, IServiceProvider serviceProvider)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetCsharpBookmarks.Value);
    SetBinding(TitleProperty, new Binding(nameof(BookmarkCollectionViewModel.Title)));
    BindingContext = viewModel;
    var title = new Label
    {
      FontSize = 22,
      FontAttributes = FontAttributes.Bold,
    };
    title.SetBinding(Label.TextProperty, nameof(BookmarkCollectionViewModel.Title));

    var error = new Label { TextColor = Colors.IndianRed };
    error.SetBinding(Label.TextProperty, nameof(BookmarkCollectionViewModel.ErrorMessage));
    var mutationError = new Label { TextColor = Colors.IndianRed };
    mutationError.SetBinding(Label.TextProperty, nameof(BookmarkCollectionViewModel.MutationErrorMessage));
    var navigationError = new Label { TextColor = Colors.IndianRed };
    navigationError.SetBinding(Label.TextProperty, nameof(BookmarkCollectionViewModel.NavigationErrorMessage));

    var rows = new CollectionView
    {
      ItemTemplate = new DataTemplate(() =>
      {
        var rowTitle = new Label { FontAttributes = FontAttributes.Bold };
        rowTitle.SetBinding(Label.TextProperty, nameof(BookmarkCollectionRow.Title));

        var subtitle = new Label { FontSize = 12, TextColor = Colors.Gray };
        subtitle.SetBinding(Label.TextProperty, nameof(BookmarkCollectionRow.Subtitle));

        var detail = new Label { FontSize = 12, TextColor = Colors.Gray };
        detail.SetBinding(Label.TextProperty, nameof(BookmarkCollectionRow.Detail));

        var content = new VerticalStackLayout
        {
          Spacing = 4,
          Children = { rowTitle, subtitle, detail },
        };
        content.SetBinding(
            SemanticProperties.DescriptionProperty,
            nameof(BookmarkCollectionRow.OpenDetailsAccessibilityLabel));
        var tap = new TapGestureRecognizer();
        tap.SetBinding(TapGestureRecognizer.CommandParameterProperty, new Binding("."));
        tap.Tapped += OnRowTapped;
        content.GestureRecognizers.Add(tap);

        var remove = new Button { VerticalOptions = LayoutOptions.Center };
        remove.SetBinding(Button.TextProperty, nameof(BookmarkCollectionRow.ActionLabel));
        remove.SetBinding(IsVisibleProperty, nameof(BookmarkCollectionRow.HasAction));
        remove.SetBinding(IsEnabledProperty, nameof(BookmarkCollectionRow.CanInvokeAction));
        remove.SetBinding(SemanticProperties.DescriptionProperty, nameof(BookmarkCollectionRow.ActionAccessibilityLabel));
        remove.SetBinding(Button.CommandParameterProperty, new Binding("."));
        remove.Clicked += OnRemoveClicked;
        Grid.SetColumn(remove, 1);

        return new Border
        {
          Stroke = Color.FromArgb("#D8DEE9"),
          StrokeThickness = 1,
          StrokeShape = new RoundRectangle { CornerRadius = 8 },
          Padding = 12,
          Content = new Grid
          {
            ColumnDefinitions =
            {
              new ColumnDefinition(GridLength.Star),
              new ColumnDefinition(GridLength.Auto),
            },
            Children = { content, remove },
          },
        };
      }),
    };
    rows.SetBinding(ItemsView.ItemsSourceProperty, nameof(BookmarkCollectionViewModel.Rows));
    rows.Footer = new BookmarkCollectionPaginationFooter(viewModel);

    Content = new Grid
    {
      Padding = 16,
      RowSpacing = 12,
      RowDefinitions =
      {
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Star),
      },
      Children = { title, error, mutationError, navigationError, rows },
    };
    Grid.SetRow(error, 1);
    Grid.SetRow(mutationError, 2);
    Grid.SetRow(navigationError, 3);
    Grid.SetRow(rows, 4);
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native page load failures are displayed in page state.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await viewModel.LoadAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  public void SetContext(BookmarkCollectionRouteContext context) => viewModel.SetContext(context);

  private async void OnRemoveClicked(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: BookmarkCollectionRow row })
    {
      ((Button)sender).IsEnabled = false;
      await viewModel.RemoveAsync(row).ConfigureAwait(true);
    }
  }

  private async void OnRowTapped(object? sender, TappedEventArgs args)
  {
    if (args.Parameter is not BookmarkCollectionRow row) return;
    try
    {
      var path = await viewModel.ResolveDestinationPathAsync(row).ConfigureAwait(true);
      if (path is not null)
      {
        await serviceProvider.GetRequiredService<AppShell>().OpenNativePathAsync(path).ConfigureAwait(true);
      }
    }
    catch (Exception)
    {
      viewModel.ReportNavigationFailure();
    }
  }
}

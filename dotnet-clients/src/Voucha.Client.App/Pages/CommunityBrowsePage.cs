using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App.Pages;

public sealed class CommunityBrowsePage : ContentPage
{
  private readonly CommunityBrowseViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private readonly Entry queryEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeSwiftCommunitiesSearchCommunities);
  private readonly Label errorLabel = new() { TextColor = Colors.Red };
  private readonly Button searchButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpSearch);
  private readonly CollectionView resultsView = new() { ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems };

  public CommunityBrowsePage(CommunityBrowseViewModel viewModel, ISessionStore sessionStore)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    sessionChangedHandler = (_, _) => MainThread.BeginInvokeOnMainThread(Render);
    BindingContext = viewModel;
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetCsharpCommunitiesCommunities.Value);
    viewModel.PropertyChanged += OnPropertyChanged;
    searchButton.Clicked += async (_, _) => await RunSearchAsync().ConfigureAwait(true);

    resultsView.ItemTemplate = new DataTemplate(() =>
    {
      var title = new Label { FontAttributes = FontAttributes.Bold };
      title.SetBinding(Label.TextProperty, nameof(CommunityBrowseRow.Name));
      var subtitle = new Label();
      subtitle.SetBinding(Label.TextProperty, nameof(CommunityBrowseRow.Slug));
      var provenance = new Label { FontSize = 12 };
      provenance.SetBinding(Label.TextProperty, nameof(CommunityBrowseRow.LocalizedProvenanceLabel));
      provenance.SetBinding(IsVisibleProperty, nameof(CommunityBrowseRow.HasProvenance));
      var staffChannel = new Label { FontSize = 12, AutomationId = "CommunityStaffChannel" };
      staffChannel.SetBinding(Label.TextProperty, nameof(CommunityBrowseRow.StaffChannelLabel));
      staffChannel.SetBinding(IsVisibleProperty, nameof(CommunityBrowseRow.HasStaffChannel));
      var counts = new Label();
      counts.SetBinding(Label.TextProperty, new Binding(nameof(CommunityBrowseRow.MemberCount), stringFormat: "{0} members"));
      var postCount = new Label();
      postCount.SetBinding(Label.TextProperty, new Binding(nameof(CommunityBrowseRow.PostCount), stringFormat: "{0} posts"));
      return new VerticalStackLayout
      {
        Spacing = 4,
        Padding = 0,
        Children = { title, subtitle, provenance, staffChannel, counts, postCount },
      };
    });

    Content = new VerticalStackLayout
    {
      Padding = 16,
      Spacing = 12,
      Children =
      {
        queryEntry,
        searchButton,
        errorLabel,
        resultsView,
      },
    };
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    sessionStore.SessionChanged += sessionChangedHandler;
    Render();
    if (viewModel.Results.Count == 0)
    {
      await RunSearchAsync().ConfigureAwait(true);
    }
  }

  protected override void OnDisappearing()
  {
    sessionStore.SessionChanged -= sessionChangedHandler;
    base.OnDisappearing();
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native page search failures are displayed in page state.")]
  private async Task RunSearchAsync()
  {
    try
    {
      await SearchAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      errorLabel.Text = ex.Message;
      errorLabel.IsVisible = true;
    }
  }

  private async Task SearchAsync()
  {
    viewModel.Query = queryEntry.Text ?? string.Empty;
    await viewModel.SearchAsync().ConfigureAwait(true);
    Render();
  }

  private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e) => Render();

  private void Render()
  {
    errorLabel.Text = viewModel.ErrorMessage ?? string.Empty;
    errorLabel.IsVisible = viewModel.HasError;
    var isStaffViewer = sessionStore.Current.Identity?.Roles?.Any(role =>
        role is "administrator" or "moderator") == true;
    resultsView.ItemsSource = viewModel.Results.Select(row => row with
    {
      IsStaffViewer = isStaffViewer,
    }).ToArray();
  }
}

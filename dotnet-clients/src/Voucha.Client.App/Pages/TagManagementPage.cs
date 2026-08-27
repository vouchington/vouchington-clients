using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Tags;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Api;
using Voucha.Client.App.Controls;

namespace Voucha.Client.App.Pages;

public sealed partial class TagManagementPage : ContentPage
{
  private readonly TagManagementViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly HorizontalStackLayout tabsRow = new() { Spacing = 8 };
  private readonly CollectionView searchResultsView = new();
  private readonly CollectionView relationsView = new();
  private readonly Entry searchEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpSearch);
  private readonly Label statusLabel = new() { TextColor = Colors.IndianRed };
  private readonly ScrollView pageScroll = new();
  private readonly HybridPaginationControl paginationControl = new() { PaginationId = "tag-relations" };

  public TagManagementPage(TagManagementViewModel viewModel, ISessionStore sessionStore)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    InitializeVotePolicyBindings();
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetCsharpTagsTags.Value);
    SetBinding(TitleProperty, new Binding(nameof(TagManagementViewModel.Title)));
    BindingContext = viewModel;
    searchEntry.SetBinding(InputView.TextProperty, nameof(TagManagementViewModel.SearchQuery), mode: BindingMode.TwoWay);
    searchResultsView.ItemTemplate = BuildSearchResultTemplate();
    searchResultsView.SetBinding(ItemsView.ItemsSourceProperty, nameof(TagManagementViewModel.SearchResults));
    relationsView.ItemTemplate = BuildRelationTemplate();
    relationsView.SetBinding(ItemsView.ItemsSourceProperty, nameof(TagManagementViewModel.Relations));
    var entityLabel = new Label { FontSize = 22, FontAttributes = FontAttributes.Bold };
    entityLabel.SetBinding(Label.TextProperty, nameof(TagManagementViewModel.EntityLabel));
    statusLabel.SetBinding(Label.TextProperty, nameof(TagManagementViewModel.ErrorMessage));
    paginationControl.SetBinding(HybridPaginationControl.HasMoreProperty, nameof(TagManagementViewModel.HasMoreRelations));
    paginationControl.SetBinding(HybridPaginationControl.IsLoadingProperty, nameof(TagManagementViewModel.IsLoadingMoreRelations));
    paginationControl.SetBinding(HybridPaginationControl.HasErrorProperty, nameof(TagManagementViewModel.HasRelationPaginationError));
    paginationControl.LoadNextPageRequested += OnLoadMoreRelationsRequested;
    var searchForm = new HorizontalStackLayout
    {
      Spacing = 8,
      Children =
      {
        searchEntry,
        BuildSearchButton(),
      },
    };
    searchForm.SetBinding(IsVisibleProperty, new Binding(nameof(TagManagementViewModel.TagLimitReached), converter: new InvertedBooleanConverter()));
    var tagLimitCta = BuildTagLimitCta();
    tagLimitCta.SetBinding(IsVisibleProperty, nameof(TagManagementViewModel.TagLimitReached));
    pageScroll.Content = new VerticalStackLayout
    {
      Padding = 16,
      Spacing = 12,
      Children =
      {
        entityLabel,
        tabsRow,
        tagLimitCta,
        searchForm,
        statusLabel,
        searchResultsView,
        UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpTagsCurrentTags),
        relationsView,
        paginationControl,
      },
    };
    Content = pageScroll;
    ConfigurePaginationViewport();
    viewModel.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName is nameof(TagManagementViewModel.Tabs) or nameof(TagManagementViewModel.SelectedTab))
      {
        RenderTabs();
      }
    };
    RenderTabs();
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native page load failures are displayed in page state.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    AttachSessionChanged();
    try
    {
      await viewModel.LoadAsync().ConfigureAwait(true);
      RenderTabs();
    }
    catch (Exception ex)
    {
      statusLabel.Text = ex.Message;
    }
  }

  public void SetContext(TagManagementRouteContext context) => viewModel.SetContext(context);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native page action failures are displayed in page state.")]
  private async Task RunSearchAsync()
  {
    try
    {
      await viewModel.SearchAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      statusLabel.Text = ex.Message;
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native page action failures are displayed in page state.")]
  private async Task AddAsync(string id)
  {
    try
    {
      await viewModel.AddTagAsync(id).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      statusLabel.Text = ex.Message;
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native page action failures are displayed in page state.")]
  private async Task VoteAsync(TagRelationRow row, ElectionVoteChoice? choice)
  {
    if (choice is null
        ? !sessionStore.Current.CanClearPublicVote(row.MyVote)
        : !sessionStore.Current.CanCreateEntityRelationVote(viewModel.Context?.EntityType == "user")) return;
    try
    {
      await viewModel.VoteRelationAsync(row.Id, choice).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      statusLabel.Text = ex.Message;
    }
  }

  private void RenderTabs()
  {
    tabsRow.Children.Clear();
    foreach (var tab in viewModel.Tabs)
    {
      var button = new Button { Text = tab.Label };
      if (viewModel.SelectedTab?.Value == tab.Value)
      {
        button.BackgroundColor = Colors.LightBlue;
      }
      button.Clicked += async (_, _) => await viewModel.SelectTabAsync(tab.Value).ConfigureAwait(true);
      tabsRow.Children.Add(button);
    }
  }

  private Button BuildSearchButton()
  {
    var button = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpSearch);
    button.Clicked += async (_, _) => await RunSearchAsync().ConfigureAwait(true);
    return button;
  }

  private static Border BuildTagLimitCta()
  {
    var title = UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpTagsLimitReachedTitle);
    var message = UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeDotnetCsharpTagsLimitReachedMessage);
    var viewPlans = UiCopy.Bind(new Button { AutomationId = "tag-limit-cta-view-plans" }, Button.TextProperty, UiMessageKey.NativeDotnetCsharpTagsViewPlans);
    viewPlans.Clicked += async (_, _) =>
    {
      if (Shell.Current is AppShell appShell)
      {
        await appShell.OpenNativePathAsync("/plans").ConfigureAwait(true);
      }
    };
    var content = new VerticalStackLayout
    {
      Spacing = 8,
      Children = { title, message, viewPlans },
    };
    return IntegrityPageViews.Card(content, "tag-limit-cta");
  }
}

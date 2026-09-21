using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class AgentConversationPage : ContentPage
{
  private readonly AgentConversationViewModel viewModel;
  private CancellationTokenSource? loadCancellation;
  private string? agentIdOrSlug;
  private string? conversationId;

  public AgentConversationPage(AgentConversationViewModel viewModel)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    SetDynamicResource(
        TitleProperty,
        UiMessageKey.NativeSwiftChatConversationTitle.Value);

    var title = new Label { FontSize = 20, FontAttributes = FontAttributes.Bold };
    title.SetBinding(Label.TextProperty, nameof(AgentConversationViewModel.ConversationDisplayTitle));
    var createdAt = new Label { AutomationId = "agent-conversation-created-at", FontSize = 11 };
    createdAt.SetBinding(Label.TextProperty, nameof(AgentConversationViewModel.ConversationCreatedAt));
    var error = new Label { TextColor = Colors.IndianRed };
    error.SetBinding(Label.TextProperty, nameof(AgentConversationViewModel.ErrorMessage));
    var retry = UiCopy.Bind(new Button { AutomationId = "agent-conversation-retry" }, Button.TextProperty, UiMessageKey.NativeDotnetEngineeringPaginationRetry);
    retry.SetBinding(IsVisibleProperty, nameof(AgentConversationViewModel.HasError));
    retry.Clicked += async (_, _) => await ReloadAsync().ConfigureAwait(true);
    var loading = new ActivityIndicator { AutomationId = "agent-conversation-loading" };
    loading.SetBinding(IsVisibleProperty, nameof(AgentConversationViewModel.IsLoading));
    loading.SetBinding(ActivityIndicator.IsRunningProperty, nameof(AgentConversationViewModel.IsLoading));
    var empty = UiCopy.Bind(new Label { AutomationId = "agent-conversation-empty" }, Label.TextProperty, UiMessageKey.NativeSwiftRouteSurfaceNoResults);
    empty.SetBinding(IsVisibleProperty, nameof(AgentConversationViewModel.IsEmpty));
    var paginationError = new Label { TextColor = Colors.IndianRed };
    paginationError.SetBinding(Label.TextProperty, nameof(AgentConversationViewModel.PaginationErrorMessage));
    var messages = new CollectionView { ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems };
    messages.SetBinding(ItemsView.ItemsSourceProperty, nameof(AgentConversationViewModel.MessageRows));
    messages.ItemTemplate = new DataTemplate(MessageTemplate);
    var refresh = UiCopy.Bind(
        new Button(),
        Button.TextProperty,
        UiMessageKey.NativeDotnetCommonRefresh);
    refresh.Clicked += async (_, _) => await ReloadAsync().ConfigureAwait(true);
    var loadOlder = new Button { AutomationId = "agent-conversation-load-older" };
    loadOlder.SetBinding(Button.TextProperty, nameof(AgentConversationViewModel.PaginationActionTitle));
    loadOlder.SetBinding(IsVisibleProperty, nameof(AgentConversationViewModel.HasMore));
    loadOlder.SetBinding(IsEnabledProperty, nameof(AgentConversationViewModel.CanLoadOlder));
    loadOlder.Clicked += async (_, _) => await viewModel.LoadOlderAsync().ConfigureAwait(true);
    var layout = new Grid
    {
      Padding = 16,
      RowDefinitions =
      {
        new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Star),
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto),
      },
    };
    AddAtRow(layout, title, 0); AddAtRow(layout, createdAt, 1);
    AddAtRow(layout, refresh, 2);
    AddAtRow(layout, loading, 3); AddAtRow(layout, error, 4); AddAtRow(layout, retry, 5); AddAtRow(layout, empty, 6);
    AddAtRow(layout, messages, 7); AddAtRow(layout, paginationError, 8); AddAtRow(layout, loadOlder, 9);
    Content = layout;
  }

  public void SetContext(string agent, string conversation)
  {
    agentIdOrSlug = agent;
    conversationId = conversation;
  }

  public async Task ApplyRouteAsync(string agent, string conversation)
  {
    SetContext(agent, conversation);
    await ReloadAsync().ConfigureAwait(true);
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await ReloadAsync().ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    loadCancellation?.Cancel();
    base.OnDisappearing();
  }

  private async Task ReloadAsync()
  {
    if (agentIdOrSlug is not { } agent || conversationId is not { } conversation) return;
    loadCancellation?.Cancel();
    loadCancellation?.Dispose();
    loadCancellation = new CancellationTokenSource();
    await viewModel.LoadAsync(agent, conversation, loadCancellation.Token).ConfigureAwait(true);
  }

  private static View MessageTemplate()
  {
    var role = new Label { FontAttributes = FontAttributes.Bold };
    role.SetBinding(Label.TextProperty, nameof(AgentTranscriptRow.Role));
    var body = new Label();
    body.SetBinding(Label.TextProperty, nameof(AgentTranscriptRow.Body));
    var timestamp = new Label { FontSize = 11 };
    timestamp.SetBinding(Label.TextProperty, nameof(AgentTranscriptRow.Timestamp));
    return new VerticalStackLayout { Padding = new Thickness(0, 6), Children = { role, body, timestamp } };
  }

  private static void AddAtRow(Grid grid, View view, int row)
  {
    Grid.SetRow(view, row);
    grid.Add(view);
  }
}

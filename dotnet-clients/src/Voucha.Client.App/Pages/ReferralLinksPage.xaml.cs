using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.ReferralLinks;

namespace Voucha.Client.App.Pages;

public partial class ReferralLinksPage : ContentPage
{
  private readonly ReferralLinksViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly ReferralLinksRouteContextStore routeContextStore;
  private readonly ReferralLinksPageBinding binding;
  private ReferralLinksMode mode;

  public ReferralLinksPage(
      ReferralLinksViewModel viewModel,
      ISessionStore sessionStore,
      ReferralLinksRouteContextStore routeContextStore)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    this.routeContextStore = routeContextStore;
    mode = ConsumeRouteMode() ?? DefaultMode(sessionStore);
    binding = new ReferralLinksPageBinding(viewModel, sessionStore, LoadCurrentAsync);
    BindingContext = binding;
    ConfigurePagination();
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void lifecycle methods must not allow load failures to escape to the dispatcher.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    binding.AttachSessionChanged();
    try
    {
      // The constructor consumes the initial one-shot route context; this re-check only catches
      // a new deep link that arrives after construction but before appearance.
      mode = ConsumeRouteMode() ?? mode;
      await LoadCurrentAsync();
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  protected override void OnDisappearing()
  {
    binding.DetachSessionChanged();
    base.OnDisappearing();
  }

  private void OnRemainingItemsThresholdReached(object? sender, EventArgs e)
  {
    LinksPaginationControl.TryLoadAutomatically();
    AnalyticsPaginationControl.TryLoadAutomatically();
  }

  private Task LoadCurrentAsync()
  {
    if (!sessionStore.Current.IsAuthenticated)
    {
      mode = ReferralLinksMode.Programs;
      return viewModel.LoadTrendingProgramsAsync();
    }

    return mode switch
    {
      ReferralLinksMode.Programs => viewModel.LoadTrendingProgramsAsync(),
      ReferralLinksMode.Analytics => viewModel.LoadAnalyticsAsync(),
      ReferralLinksMode.Mutual => viewModel.LoadFeedAsync(mutual: true),
      ReferralLinksMode.Mine => viewModel.LoadMineAsync(),
      _ => viewModel.LoadFeedAsync(),
    };
  }

  private async void OnFollowingClicked(object? sender, EventArgs e) =>
      await RunPageActionAsync(() => SetModeAndLoadAsync(ReferralLinksMode.Following));

  private async void OnMutualClicked(object? sender, EventArgs e) =>
      await RunPageActionAsync(() => SetModeAndLoadAsync(ReferralLinksMode.Mutual));

  private async void OnMineClicked(object? sender, EventArgs e) =>
      await RunPageActionAsync(() => SetModeAndLoadAsync(ReferralLinksMode.Mine));

  private async void OnProgramsClicked(object? sender, EventArgs e) =>
      await RunPageActionAsync(() => SetModeAndLoadAsync(ReferralLinksMode.Programs));

  private async void OnAnalyticsClicked(object? sender, EventArgs e) =>
      await RunPageActionAsync(() => SetModeAndLoadAsync(ReferralLinksMode.Analytics));

  private async void OnAddClicked(object? sender, EventArgs e)
  {
    await RunPageActionAsync(async () =>
    {
      var query = await DisplayPromptAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsReferralProgram),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsSearchPrograms),
          initialValue: string.Empty);
      if (string.IsNullOrWhiteSpace(query)) return;

      var programs = await viewModel.SearchReferralProgramsAsync(query);
      if (programs.Count == 0)
      {
        await DisplayAlertAsync(
            UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsNoProgramsFound),
            UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsNoProgramsMatched),
            UiCopy.Localize(UiMessageKey.NativeDotnetCsharpOk));
        return;
      }

      var selectedName = await DisplayActionSheetAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsChooseProgram),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel),
          null,
          programs.Select(program => program.Name).ToArray());
      var selectedProgram = programs.FirstOrDefault(program => program.Name == selectedName);
      if (selectedProgram is null) return;

      var validationInfo = await viewModel.FetchValidationInfoAsync(selectedProgram.Id);
      var urlValue = await DisplayPromptAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsReferralUrl),
          validationInfo.UserHelpText ?? UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsPasteReferralUrl),
          placeholder: validationInfo.ExampleUrls.Count > 0 ? validationInfo.ExampleUrls[0] : UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsReferralUrl),
          initialValue: string.Empty);
      if (!Uri.TryCreate(urlValue, UriKind.Absolute, out var url)) return;

      var label = await DisplayPromptAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsLabel),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsOptionalLabel));
      if (label is null) return;
      await viewModel.CreateAsync(selectedProgram.Id, url, label);
      mode = ReferralLinksMode.Mine;
    });
  }

  private async void OnRenameClicked(object? sender, EventArgs e)
  {
    await RunPageActionAsync(async () =>
    {
      if (RowFromSender(sender) is not { } row) return;
      var label = await DisplayPromptAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsRename),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsLabel),
          initialValue: row.Title);
      if (label is null) return;
      await viewModel.RenameAsync(row.Id, label);
      mode = ReferralLinksMode.Mine;
    });
  }

  private async void OnToggleActiveClicked(object? sender, EventArgs e)
  {
    await RunPageActionAsync(async () =>
    {
      if (RowFromSender(sender) is not { } row) return;
      await viewModel.SetActiveAsync(row.Id, !row.IsActive);
      mode = ReferralLinksMode.Mine;
    });
  }

  private async Task SetModeAndLoadAsync(ReferralLinksMode nextMode)
  {
    mode = nextMode;
    await LoadCurrentAsync();
  }

  private static ReferralLinksMode DefaultMode(ISessionStore sessionStore) =>
      sessionStore.Current.IsAuthenticated ? ReferralLinksMode.Following : ReferralLinksMode.Programs;

  private ReferralLinksMode? ConsumeRouteMode() => routeContextStore.Consume()?.Mode;

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not allow failures to escape to the dispatcher.")]
  private async Task RunPageActionAsync(Func<Task> action)
  {
    try
    {
      await action();
    }
    catch (Exception ex)
    {
      await DisplayAlertAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpError),
          ex.Message,
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpOk));
    }
  }

  private static ReferralLinkRow? RowFromSender(object? sender) =>
      (sender as BindableObject)?.BindingContext as ReferralLinkRow;
}

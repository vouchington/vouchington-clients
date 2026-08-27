using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract class IntegrityPenaltiesPageBase<TPenalty> : ContentPage
{
  private readonly IntegrityPenaltyLedgerViewModel<TPenalty> viewModel;
  private readonly string automationPrefix;
  private readonly Picker statusPicker = new();
  private readonly Label error = new() { TextColor = Colors.IndianRed };
  private readonly VerticalStackLayout rows = new() { Spacing = 12 };
  private readonly Button loadMore = new();
  private readonly Button retry = new() { AutomationId = "integrity-penalties-retry" };
  private string? confirmingId;
  private CancellationTokenSource? cancellation;
  private bool applyingStatus;

  protected IntegrityPenaltiesPageBase(
      IntegrityPenaltyLedgerViewModel<TPenalty> viewModel,
      UiMessageKey titleKey,
      string domain,
      string flagsPath,
      string penaltiesPath)
  {
    this.viewModel = viewModel;
    automationPrefix = $"{domain}-integrity-penalties";
    BindingContext = viewModel;
    SetDynamicResource(TitleProperty, titleKey.Value);
    statusPicker.AutomationId = $"{automationPrefix}-status";
    statusPicker.SetDynamicResource(Picker.TitleProperty, UiMessageKey.NativeSwiftIntegrityStatus.Value);
    statusPicker.ItemsSource = viewModel.AvailableStatuses.Select(StatusLabel).ToArray();
    statusPicker.SelectedIndexChanged += async (_, _) => await SelectStatusAsync().ConfigureAwait(true);
    loadMore.AutomationId = $"{automationPrefix}-load-more";
    loadMore.SetDynamicResource(Button.TextProperty, UiMessageKey.NativeSwiftIntegrityLoadMore.Value);
    loadMore.Clicked += async (_, _) => await viewModel.LoadMoreAsync(Token()).ConfigureAwait(true);
    retry.SetDynamicResource(Button.TextProperty, UiMessageKey.NativeSwiftIntegrityRetry.Value);
    retry.Clicked += async (_, _) => await viewModel.ReloadAsync(Token()).ConfigureAwait(true);
    viewModel.PropertyChanged += (_, _) => Render();
    Content = new ScrollView { Content = Layout(titleKey, flagsPath, penaltiesPath) };
    Render();
  }

  public Task ReloadAsync() => viewModel.ReloadAsync(Token());
  protected abstract IReadOnlyList<IntegrityPenaltyRow> PresentedRows { get; }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await viewModel.LoadAsync(Token()).ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    cancellation?.Cancel();
    cancellation?.Dispose();
    cancellation = null;
    base.OnDisappearing();
  }

  private View Layout(UiMessageKey titleKey, string flagsPath, string penaltiesPath)
  {
    var flags = NavigationButton(UiMessageKey.NativeSwiftIntegrityFlags, flagsPath, $"{automationPrefix}-flags");
    var penalties = NavigationButton(UiMessageKey.NativeSwiftIntegrityPenalties, penaltiesPath, $"{automationPrefix}-penalties");
    var title = new Label { FontSize = 24, FontAttributes = FontAttributes.Bold };
    title.SetDynamicResource(Label.TextProperty, titleKey.Value);
    return new VerticalStackLayout
    {
      Padding = 16, Spacing = 12,
      Children = { title, new HorizontalStackLayout { Children = { flags, penalties } },
        statusPicker, error, retry, rows, loadMore },
    };
  }

  private Button NavigationButton(UiMessageKey key, string path, string id)
  {
    var button = new Button { CommandParameter = path, AutomationId = id };
    button.SetDynamicResource(Button.TextProperty, key.Value);
    button.Clicked += async (_, _) =>
    {
      if (Shell.Current is AppShell shell)
        await shell.OpenNativePathAsync(path).ConfigureAwait(true);
    };
    return button;
  }

  private async Task SelectStatusAsync()
  {
    if (applyingStatus || statusPicker.SelectedIndex < 0) return;
    confirmingId = null;
    await viewModel.SelectStatusAsync(
        viewModel.AvailableStatuses[statusPicker.SelectedIndex], Token()).ConfigureAwait(true);
  }

  private void Render()
  {
    error.Text = viewModel.ErrorMessage;
    retry.IsVisible = viewModel.State == Voucha.Client.Core.Support.LoadState.Error;
    applyingStatus = true;
    statusPicker.ItemsSource = viewModel.AvailableStatuses.Select(StatusLabel).ToArray();
    statusPicker.SelectedIndex = Array.IndexOf(viewModel.AvailableStatuses.ToArray(), viewModel.Status);
    applyingStatus = false;
    loadMore.IsVisible = viewModel.HasMore;
    loadMore.IsEnabled = viewModel.CanLoadMore;
    rows.Children.Clear();
    if (viewModel.IsLoading) rows.Children.Add(new ActivityIndicator { IsRunning = true, AutomationId = $"{automationPrefix}-loading" });
    else if (viewModel.IsEmpty) rows.Children.Add(LocalizedLabel(UiMessageKey.NativeSwiftIntegrityNoPenaltiesMessage, $"{automationPrefix}-empty"));
    else foreach (var row in PresentedRows) rows.Children.Add(PenaltyCard(row));
  }

  private View PenaltyCard(IntegrityPenaltyRow row)
  {
    var content = IntegrityPageViews.CardLines(
        UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityPenaltyId), row.Id,
        UiCopy.Format(UiMessageKey.NativeSwiftIntegrityUserId, ("id", row.UserId)),
        UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityReason), LocalizedReason(row.Reason),
        row.SourceFlagId is null ? string.Empty : UiCopy.Localize(UiMessageKey.NativeSwiftIntegritySourceFlag),
        row.SourceFlagId ?? string.Empty,
        UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityCreated), UiCopy.FormatDateTime(row.CreatedAt),
        row.CreatedById is null ? string.Empty : UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityCreatedBy),
        row.CreatedById ?? string.Empty,
        row.Multiplier is null ? string.Empty : UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityMultiplier),
        row.Multiplier is null ? string.Empty : UiCopy.FormatPercent((decimal)row.Multiplier.Value),
        UiCopy.Localize(row.RevokedAt is null ? UiMessageKey.NativeSwiftIntegrityActive : UiMessageKey.NativeSwiftIntegrityRevoked),
        row.RevokedAt is null ? string.Empty : UiCopy.FormatDateTime(row.RevokedAt.Value),
        row.RevokedById is null ? string.Empty : UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityRevokedBy),
        row.RevokedById ?? string.Empty);
    content.Children.Insert(2, IntegrityPageViews.Entity(new(
        UiText.Localized(UiMessageKey.NativeSwiftIntegrityUserId, ("id", row.UserId)),
        row.UserId, $"/user/{row.UserId}")));
    if (viewModel.CanRevoke(row.Id) || confirmingId == row.Id)
      content.Children.Add(RevokeButton(row.Id));
    if (viewModel.NeedsReconciliation(row.Id)) content.Children.Add(ReconcileButton(row.Id));
    if (viewModel.ActionError(row.Id) is { } actionError)
      content.Children.Add(new Label { Text = actionError, TextColor = Colors.IndianRed });
    return IntegrityPageViews.Card(content, $"{automationPrefix}-{row.Id}");
  }

  private Button RevokeButton(string id)
  {
    var button = new Button { AutomationId = $"{automationPrefix}-revoke-{id}" };
    button.SetDynamicResource(Button.TextProperty, (confirmingId == id
        ? UiMessageKey.NativeSwiftIntegrityConfirmRevoke
        : UiMessageKey.NativeSwiftIntegrityRevoke).Value);
    button.Clicked += async (_, _) =>
    {
      if (confirmingId != id) { confirmingId = id; Render(); return; }
      confirmingId = null;
      await viewModel.RevokeAsync(id, Token()).ConfigureAwait(true);
    };
    return button;
  }

  private Button ReconcileButton(string id)
  {
    var button = new Button { AutomationId = $"{automationPrefix}-reconcile-{id}" };
    button.SetDynamicResource(Button.TextProperty, UiMessageKey.NativeSwiftIntegrityReconcile.Value);
    button.Clicked += async (_, _) => await viewModel.ReconcileAsync(id).ConfigureAwait(true);
    return button;
  }

  private static Label LocalizedLabel(UiMessageKey key, string id)
  {
    var label = new Label { AutomationId = id };
    label.SetDynamicResource(Label.TextProperty, key.Value);
    return label;
  }

  private string StatusLabel(IntegrityPenaltyStatus status) =>
      UiCopy.Localize(status switch
      {
        IntegrityPenaltyStatus.Active => UiMessageKey.NativeSwiftIntegrityActive,
        IntegrityPenaltyStatus.Revoked => UiMessageKey.NativeSwiftIntegrityRevoked,
        _ => UiMessageKey.NativeSwiftIntegrityAll,
      });

  private static string LocalizedReason(string reason) => reason switch
  {
    "mass_report_campaign" => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityMassReportCampaign),
    "voting_ring" => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityVotingRing),
    _ => reason,
  };

  private CancellationToken Token() => (cancellation ??= new()).Token;
}

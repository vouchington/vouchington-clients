using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

internal sealed class ModerationAppealCardView : VerticalStackLayout
{
  private readonly ModerationAppealsViewModel viewModel;
  private readonly Label heading = new() { FontAttributes = FontAttributes.Bold };
  private readonly Label context = new() { FontSize = 12 };
  private readonly Label reason = new();
  private readonly Label recommendation = new();
  private readonly Label aiInternalResponse = new();
  private readonly Label internalNotes = new();
  private readonly Label lifecycle = new() { FontSize = 12 };
  private readonly Editor publicResponse = UiCopy.Bind(
      new Editor { AutoSize = EditorAutoSizeOption.TextChanges },
      Editor.PlaceholderProperty,
      UiMessageKey.NativeSwiftModerationAppealsPublicResponse);
  private readonly Button save = UiCopy.Bind(
      new Button(),
      Button.TextProperty,
      UiMessageKey.CommonSave);
  private readonly Button approve = UiCopy.Bind(
      new Button(),
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsApprove);
  private readonly Button deliver = UiCopy.Bind(
      new Button(),
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsSend);
  private readonly Button rerun = UiCopy.Bind(
      new Button(),
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsRerunAi);
  private readonly Button accept = UiCopy.Bind(
      new Button(),
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsAccept);
  private readonly Button reduce = UiCopy.Bind(
      new Button(),
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsReduce);
  private readonly Button deny = UiCopy.Bind(
      new Button(),
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsDeny);
  private readonly Button refreshDelivery = UiCopy.Bind(
      new Button(),
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsRefreshDelivery);

  public ModerationAppealCardView(ModerationAppealsViewModel viewModel)
  {
    this.viewModel = viewModel;
    Padding = 12;
    Spacing = 8;
    Children.Add(heading);
    Children.Add(context);
    Children.Add(reason);
    Children.Add(recommendation);
    Children.Add(aiInternalResponse);
    Children.Add(internalNotes);
    Children.Add(lifecycle);
    Children.Add(UiCopy.Bind(
        new Label { FontAttributes = FontAttributes.Bold },
        Label.TextProperty,
        UiMessageKey.NativeSwiftModerationAppealsPublicResponse));
    Children.Add(publicResponse);
    Children.Add(new HorizontalStackLayout { Spacing = 6, Children = { save, approve, deliver, rerun } });
    Children.Add(new HorizontalStackLayout { Spacing = 6, Children = { accept, reduce, deny } });
    Children.Add(refreshDelivery);
    BindingContextChanged += (_, _) => Populate();
    publicResponse.TextChanged += (_, _) => UpdateDraft();
    save.Clicked += async (_, _) => await WithAppeal(async (appeal, token) =>
    {
      _ = await viewModel.SaveDraftAsync(appeal, token).ConfigureAwait(true);
    }).ConfigureAwait(true);
    approve.Clicked += async (_, _) => await WithAppeal(viewModel.ApproveAsync).ConfigureAwait(true);
    deliver.Clicked += async (_, _) => await WithAppeal(viewModel.DeliverAsync).ConfigureAwait(true);
    rerun.Clicked += async (_, _) => await WithAppeal(viewModel.RerunAsync).ConfigureAwait(true);
    accept.Clicked += async (_, _) => await ResolveAsync(ModerationAppealAction.Accept).ConfigureAwait(true);
    reduce.Clicked += async (_, _) => await ResolveAsync(ModerationAppealAction.Reduce).ConfigureAwait(true);
    deny.Clicked += async (_, _) => await ResolveAsync(ModerationAppealAction.Deny).ConfigureAwait(true);
    refreshDelivery.Clicked += async (_, _) => await WithAppeal(viewModel.RefreshDeliveryStatusAsync).ConfigureAwait(true);
    Loaded += OnLoaded;
    Unloaded += OnUnloaded;
  }

  private ModerationAppeal? Appeal => BindingContext as ModerationAppeal;

  private void Populate()
  {
    if (Appeal is not { } appeal) return;
    heading.Text = ModerationAppealCardPresentation.Heading(appeal);
    context.Text = ModerationAppealCardPresentation.Context(appeal);
    reason.Text = ModerationAppealCardPresentation.Reason(appeal);
    recommendation.Text = ModerationAppealCardPresentation.Recommendation(appeal);
    PopulateReadOnly(
        aiInternalResponse,
        UiMessageKey.NativeSwiftModerationAppealsInternalAiResponse,
        appeal.AiInternalResponse);
    PopulateReadOnly(
        internalNotes,
        UiMessageKey.NativeSwiftModerationAppealsInternalNotes,
        appeal.InternalNotes);
    lifecycle.Text = ModerationAppealCardPresentation.Lifecycle(appeal);
    publicResponse.Text = viewModel.DraftFor(appeal);
    UpdateActions();
  }

  private void UpdateDraft()
  {
    if (Appeal is not { } appeal) return;
    viewModel.SetDraft(appeal, publicResponse.Text ?? string.Empty);
    UpdateActions();
  }

  private void UpdateActions()
  {
    if (Appeal is not { } appeal) return;
    publicResponse.IsEnabled = viewModel.CanEdit(appeal);
    save.IsEnabled = viewModel.CanEdit(appeal);
    approve.IsEnabled = viewModel.CanApprove(appeal);
    deliver.IsEnabled = viewModel.CanDeliver(appeal);
    rerun.IsEnabled = viewModel.CanRerun(appeal);
    accept.IsEnabled = viewModel.CanResolve(appeal, ModerationAppealAction.Accept);
    reduce.IsEnabled = viewModel.CanResolve(appeal, ModerationAppealAction.Reduce);
    deny.IsEnabled = viewModel.CanResolve(appeal, ModerationAppealAction.Deny);
    refreshDelivery.IsVisible = viewModel.IsDeliveryAmbiguous(appeal);
  }

  private async Task WithAppeal(Func<ModerationAppeal, CancellationToken, Task> action)
  {
    if (Appeal is { } appeal) await action(appeal, default).ConfigureAwait(true);
  }

  private Task ResolveAsync(ModerationAppealAction action) =>
      Appeal is { } appeal ? viewModel.ResolveAsync(appeal, action) : Task.CompletedTask;

  private void ViewModelChanged(object? sender, PropertyChangedEventArgs args) => Populate();

  private void OnLoaded(object? sender, EventArgs args)
  {
    viewModel.PropertyChanged -= ViewModelChanged;
    viewModel.PropertyChanged += ViewModelChanged;
    Populate();
  }

  private void OnUnloaded(object? sender, EventArgs args) =>
      viewModel.PropertyChanged -= ViewModelChanged;

  private static void PopulateReadOnly(
      Label label,
      UiMessageKey heading,
      string? value)
  {
    label.IsVisible = !string.IsNullOrWhiteSpace(value);
    label.Text = label.IsVisible
        ? ModerationAppealCardPresentation.LabelValue(heading, value!)
        : string.Empty;
  }
}

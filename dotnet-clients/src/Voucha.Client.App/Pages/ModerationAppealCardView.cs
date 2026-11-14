using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Content;
namespace Voucha.Client.App.Pages;

internal sealed partial class ModerationAppealCardView : VerticalStackLayout
{
  private readonly ModerationAppealsViewModel viewModel;
  private readonly Func<CancellationToken> lifecycleToken;
  private readonly ModerationActionErrorLabel actionError;
  private readonly Label heading = new() { FontAttributes = FontAttributes.Bold };
  private readonly Label context = new() { FontSize = 12 };
  private readonly Label removalTitle = new();
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
      new Button { AutomationId = "moderation-appeal-save" },
      Button.TextProperty,
      UiMessageKey.CommonSave);
  private readonly Button approve = UiCopy.Bind(
      new Button { AutomationId = "moderation-appeal-approve" },
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsApprove);
  private readonly Button deliver = UiCopy.Bind(
      new Button { AutomationId = "moderation-appeal-deliver" },
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsSend);
  private readonly Button rerun = UiCopy.Bind(
      new Button { AutomationId = "moderation-appeal-rerun" },
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsRerunAi);
  private readonly Button refreshAIRerun = UiCopy.Bind(
      new Button { AutomationId = "moderation-appeal-refreshAIRerun" },
      Button.TextProperty,
      UiMessageKey.NativeSwiftReviewDisputesRefreshStatus);
  private readonly Label aiRerunPending = UiCopy.Bind(
      new Label(),
      Label.TextProperty,
      UiMessageKey.NativeSwiftReviewDisputesRerunStillProcessing);
  private readonly Button accept = UiCopy.Bind(
      new Button { AutomationId = "moderation-appeal-accept" },
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsAccept);
  private readonly Button reduce = UiCopy.Bind(
      new Button { AutomationId = "moderation-appeal-reduce" },
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsReduce);
  private readonly Button deny = UiCopy.Bind(
      new Button { AutomationId = "moderation-appeal-deny" },
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsDeny);
  private readonly Button refreshDelivery = UiCopy.Bind(
      new Button { AutomationId = "moderation-appeal-refreshDelivery" },
      Button.TextProperty,
      UiMessageKey.NativeSwiftModerationAppealsRefreshDelivery);

  public ModerationAppealCardView(ModerationAppealsViewModel viewModel, Func<CancellationToken>? lifecycleToken = null)
  {
    this.viewModel = viewModel;
    this.lifecycleToken = lifecycleToken ?? (() => default);
    actionError = new(viewModel, nameof(ModerationAppealsViewModel.ErrorMessage), nameof(ModerationAppealsViewModel.HasError), "moderation-appeal-action-error");
    Padding = 12;
    Spacing = 8;
    Children.Add(actionError);
    Children.Add(heading);
    Children.Add(context);
    Children.Add(removalTitle);
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
    Children.Add(refreshAIRerun);
    Children.Add(aiRerunPending);
    Children.Add(new HorizontalStackLayout { Spacing = 6, Children = { accept, reduce, deny } });
    Children.Add(refreshDelivery);
    BindingContextChanged += (_, _) => Populate();
    publicResponse.TextChanged += (_, _) => UpdateDraft();
    ConfigureActions();
    Loaded += OnLoaded;
    Unloaded += OnUnloaded;
  }
  private ModerationAppeal? Appeal => viewModel.CanAccess && BindingContext is ModerationAppeal selected
      ? viewModel.Appeals.FirstOrDefault(item => item.Id == selected.Id) ?? selected : null;

  private void Populate()
  {
    if (!viewModel.CanAccess || BindingContext is not ModerationAppeal)
    {
      actionError.ClearPrivateState();
      BindingContext = null;
      Children.Clear();
      return;
    }
    if (Appeal is not { } appeal) return;
    heading.Text = ModerationAppealCardPresentation.Heading(appeal);
    context.Text = ModerationAppealCardPresentation.Context(appeal);
    removalTitle.Text = (appeal.TargetContext as ModerationAppealPostRemovalContext)?.Title;
    removalTitle.IsVisible = !string.IsNullOrWhiteSpace(removalTitle.Text);
    var language = appeal.TargetContext as ModerationAppealPostRemovalContext;
    var resolvedLanguage = AuthoredContentLanguage.Resolve(
        language?.DeclaredLanguage, language?.LinguaRsDetectedLanguage);
    removalTitle.FlowDirection = resolvedLanguage.Direction switch
    {
      AuthoredTextDirection.RightToLeft => FlowDirection.RightToLeft,
      AuthoredTextDirection.LeftToRight => FlowDirection.LeftToRight,
      _ => FlowDirection.MatchParent,
    };
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
    refreshAIRerun.IsVisible = viewModel.IsAIRerunPending(appeal);
    refreshAIRerun.IsEnabled = viewModel.CanRefreshAIRerun(appeal);
    aiRerunPending.IsVisible = refreshAIRerun.IsVisible;
    accept.IsEnabled = viewModel.CanResolve(appeal, ModerationAppealAction.Accept);
    reduce.IsEnabled = viewModel.CanResolve(appeal, ModerationAppealAction.Reduce);
    deny.IsEnabled = viewModel.CanResolve(appeal, ModerationAppealAction.Deny);
    refreshDelivery.IsVisible = viewModel.IsDeliveryAmbiguous(appeal);
  }

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

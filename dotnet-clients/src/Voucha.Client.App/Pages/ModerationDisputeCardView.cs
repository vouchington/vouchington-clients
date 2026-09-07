using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Content;

namespace Voucha.Client.App.Pages;

internal sealed partial class ModerationDisputeCardView : VerticalStackLayout
{
  private readonly ModerationDisputesViewModel viewModel;
  private readonly Func<CancellationToken> lifecycleToken;
  private readonly Label heading = new() { FontAttributes = FontAttributes.Bold };
  private readonly Label context = new() { FontSize = 12 };
  private readonly Label postContent = new();
  private readonly Label claim = new();
  private readonly Label recommendation = new();
  private readonly Label lifecycle = new() { FontSize = 12 };
  private readonly Label aiInternal = new();
  private readonly Label internalNotes = new();
  private readonly Editor publicResponse = UiCopy.Bind(
      new Editor { AutoSize = EditorAutoSizeOption.TextChanges },
      Editor.PlaceholderProperty,
      UiMessageKey.NativeSwiftReviewDisputesPublicResponse);
  private readonly Editor annotation = UiCopy.Bind(
      new Editor
      {
        AutoSize = EditorAutoSizeOption.TextChanges,
        MaxLength = ModerationDisputesViewModel.AnnotationBodyMaximumLength,
      },
      Editor.PlaceholderProperty,
      UiMessageKey.NativeSwiftReviewDisputesAnnotation);
  private readonly Button save = ButtonFor(
      UiMessageKey.NativeSwiftReviewDisputesSave,
      "review-dispute-save");
  private readonly Button approve = ButtonFor(
      UiMessageKey.NativeSwiftReviewDisputesApprove,
      "review-dispute-approve");
  private readonly Button deliver = ButtonFor(
      UiMessageKey.NativeSwiftReviewDisputesDeliver,
      "review-dispute-deliver");
  private readonly Button rerun = ButtonFor(
      UiMessageKey.NativeSwiftReviewDisputesRerunAi,
      "review-dispute-rerun");
  private readonly Button remove = ButtonFor(
      UiMessageKey.NativeSwiftModerationReportsRemoveContent,
      "review-dispute-remove");
  private readonly Button annotate = ButtonFor(
      UiMessageKey.NativeSwiftReviewDisputesAnnotation,
      "review-dispute-annotate");
  private readonly Button dismiss = ButtonFor(
      UiMessageKey.NativeSwiftReviewDisputesDismiss,
      "review-dispute-dismiss");
  private readonly Button refresh = ButtonFor(
      UiMessageKey.NativeSwiftReviewDisputesRefreshStatus,
      "review-dispute-refresh");

  public ModerationDisputeCardView(
      ModerationDisputesViewModel viewModel,
      Func<CancellationToken> lifecycleToken)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.lifecycleToken =
        lifecycleToken ?? throw new ArgumentNullException(nameof(lifecycleToken));
    Padding = 12;
    Spacing = 8;
    Children.Add(heading);
    Children.Add(context);
    Children.Add(postContent);
    Children.Add(claim);
    Children.Add(recommendation);
    Children.Add(lifecycle);
    Children.Add(aiInternal);
    Children.Add(internalNotes);
    Children.Add(UiCopy.Bind(
        new Label { FontAttributes = FontAttributes.Bold },
        Label.TextProperty,
        UiMessageKey.NativeSwiftReviewDisputesPublicResponse));
    Children.Add(publicResponse);
    Children.Add(UiCopy.Bind(
        new Label { FontAttributes = FontAttributes.Bold },
        Label.TextProperty,
        UiMessageKey.NativeSwiftReviewDisputesAnnotation));
    Children.Add(annotation);
    Children.Add(new HorizontalStackLayout
    {
      Spacing = 6,
      Children = { save, approve, deliver, rerun },
    });
    Children.Add(new HorizontalStackLayout
    {
      Spacing = 6,
      Children = { remove, annotate, dismiss },
    });
    Children.Add(refresh);
    BindingContextChanged += (_, _) => Populate();
    publicResponse.TextChanged += (_, _) =>
    {
      if (Dispute is not { } dispute) return;
      viewModel.SetPublicResponseDraft(dispute, publicResponse.Text ?? string.Empty);
      UpdateActions();
    };
    annotation.TextChanged += (_, _) =>
    {
      if (Dispute is not { } dispute) return;
      viewModel.SetAnnotationDraft(dispute, annotation.Text ?? string.Empty);
      UpdateActions();
    };
    ConfigureActions();
    Loaded += OnLoaded;
    Unloaded += OnUnloaded;
  }

  private ModerationDispute? Dispute => BindingContext as ModerationDispute;

  private void Populate()
  {
    if (Dispute is not { } dispute) return;
    AutomationId = $"review-dispute-{dispute.Id}";
    heading.Text = ModerationDisputeCardPresentation.Heading(dispute);
    context.Text = ModerationDisputeCardPresentation.Context(dispute);
    postContent.Text = dispute.PostContent?.Text;
    postContent.IsVisible = !string.IsNullOrWhiteSpace(postContent.Text);
    var language = AuthoredContentLanguage.Resolve(
        dispute.PostContent?.DeclaredLanguage, dispute.PostContent?.LinguaRsDetectedLanguage);
    if (language.Direction is { } direction)
      postContent.FlowDirection = direction == AuthoredTextDirection.RightToLeft
          ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    else
      postContent.FlowDirection = FlowDirection.MatchParent;
    claim.Text = ModerationDisputeCardPresentation.Claim(dispute);
    recommendation.Text = ModerationDisputeCardPresentation.Recommendation(dispute);
    recommendation.IsVisible = !string.IsNullOrWhiteSpace(recommendation.Text);
    lifecycle.Text = ModerationDisputeCardPresentation.Lifecycle(dispute);
    PopulateReadOnly(
        aiInternal,
        UiMessageKey.NativeSwiftReviewDisputesInternalAiResponse,
        dispute.AiInternalResponse);
    PopulateReadOnly(
        internalNotes,
        UiMessageKey.NativeSwiftReviewDisputesInternalNotes,
        dispute.InternalNotes);
    publicResponse.Text = viewModel.PublicResponseDraftFor(dispute);
    annotation.Text = viewModel.AnnotationDraftFor(dispute);
    UpdateActions();
  }

  private void UpdateActions()
  {
    if (Dispute is not { } dispute) return;
    publicResponse.IsEnabled = viewModel.CanEdit(dispute);
    annotation.IsEnabled = viewModel.CanEditAnnotation(dispute);
    save.IsEnabled = viewModel.CanEdit(dispute);
    approve.IsEnabled = viewModel.CanApprove(dispute);
    deliver.IsEnabled = viewModel.CanDeliver(dispute);
    rerun.IsEnabled = viewModel.CanRerun(dispute);
    remove.IsEnabled = viewModel.CanResolve(dispute, ModerationDisputeResolutionAction.Remove);
    annotate.IsEnabled = viewModel.CanResolve(dispute, ModerationDisputeResolutionAction.Annotate);
    dismiss.IsEnabled = viewModel.CanResolve(dispute, ModerationDisputeResolutionAction.Dismiss);
    refresh.IsVisible = viewModel.IsMutationAmbiguous(dispute);
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

  private static Button ButtonFor(UiMessageKey key, string id) =>
      UiCopy.Bind(new Button { AutomationId = id }, Button.TextProperty, key);

  private static void PopulateReadOnly(Label label, UiMessageKey key, string? value)
  {
    label.IsVisible = !string.IsNullOrWhiteSpace(value);
    label.Text = label.IsVisible
        ? ModerationDisputeCardPresentation.Labeled(key, value!)
        : string.Empty;
  }
}

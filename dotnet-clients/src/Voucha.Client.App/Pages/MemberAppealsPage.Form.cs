using System.Diagnostics;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

public sealed partial class MemberAppealsPage
{
  private View BuildForm()
  {
    var draft = viewModel.ActiveDraft;
    var picker = new Picker
    {
      AutomationId = "member-appeal-reason",
      Title = UiCopy.Localize(UiMessageKey.NativeSwiftModerationAppealsMemberSelectReason),
      ItemsSource = ReasonLabels,
      SelectedIndex = draft.Reason is null ? -1 : (int)draft.Reason.Value,
    };
    picker.SelectedIndexChanged += (_, _) =>
        viewModel.SetReason(picker.SelectedIndex < 0
            ? null
            : (ModerationAppealReason)picker.SelectedIndex);
    var details = new Editor
    {
      AutomationId = "member-appeal-details",
      Text = draft.Details,
      MaxLength = 3800,
      AutoSize = EditorAutoSizeOption.TextChanges,
    };
    details.TextChanged += (_, args) => viewModel.SetDetails(args.NewTextValue ?? "");
    var submit = UiCopy.Bind(
        new Button
        {
          AutomationId = "member-appeal-submit",
          IsEnabled = !submissionOperationActive,
        },
        Button.TextProperty,
        UiMessageKey.NativeSwiftModerationAppealsMemberSubmit);
    submit.Clicked += SubmitAsync;
    var cancel = UiCopy.Bind(
        new Button { AutomationId = "member-appeal-cancel" },
        Button.TextProperty,
        UiMessageKey.NativeSwiftModerationAppealsMemberCancel);
    cancel.Clicked += (_, _) =>
    {
      submissionCancellation?.Cancel();
      viewModel.CancelAppeal();
      Content = BuildContent();
    };
    return new VerticalStackLayout
    {
      AutomationId = "member-appeal-form",
      Spacing = 8,
      Children = { picker, details, submit, cancel },
    };
  }

  private static string[] ReasonLabels =>
  [
    UiCopy.Localize(UiMessageKey.NativeSwiftModerationAppealsMemberReasonIncorrectFacts),
    UiCopy.Localize(UiMessageKey.NativeSwiftModerationAppealsMemberReasonWrongRule),
    UiCopy.Localize(UiMessageKey.NativeSwiftModerationAppealsMemberReasonContextMissing),
    UiCopy.Localize(UiMessageKey.NativeSwiftModerationAppealsMemberReasonDisproportionate),
    UiCopy.Localize(UiMessageKey.NativeSwiftModerationAppealsMemberReasonOther),
  ];

  private async void SubmitAsync(object? sender, EventArgs args)
  {
    await RunSubmissionAsync().ConfigureAwait(true);
  }

  private async Task RunSubmissionAsync()
  {
    if (submissionOperationActive || viewModel.IsSubmitting) return;
    EnsureLifetime();
    var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
        lifetimeCancellation.Token);
    submissionCancellation = cancellation;
    submissionOperationActive = true;
    if (FindSubmitButton() is { } button) button.IsEnabled = false;
    try
    {
      var token = await turnstileTokenProvider.GetTokenAsync(
          cancellation.Token).ConfigureAwait(true);
      cancellation.Token.ThrowIfCancellationRequested();
      await viewModel.SubmitAsync(
          token, cancellation.Token).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
      Debug.WriteLine(ex);
      viewModel.SetSubmissionFailure();
    }
    finally
    {
      if (ReferenceEquals(submissionCancellation, cancellation))
        submissionCancellation = null;
      cancellation.Dispose();
      submissionOperationActive = false;
      if (!lifetimeCancellation.IsCancellationRequested) Content = BuildContent();
    }
  }

  private Button? FindSubmitButton() =>
      this.GetVisualTreeDescendants().OfType<Button>()
          .SingleOrDefault(item => item.AutomationId == "member-appeal-submit");

  private View TargetCard(MemberAppealTarget target)
  {
    var button = UiCopy.Bind(
        new Button { AutomationId = $"member-appeal-file-{target.Id}" },
        Button.TextProperty,
        UiMessageKey.NativeSwiftModerationAppealsMemberFileAppeal);
    button.Clicked += (_, _) =>
    {
      viewModel.BeginAppeal(target);
      Content = BuildContent();
    };
    return new Border
    {
      Padding = 12,
      Content = new VerticalStackLayout
      {
        Children =
        {
          new Label
          {
            Text = UiCopy.Resolve(TargetText(target)),
            FontAttributes = FontAttributes.Bold,
          },
          new Label { Text = UiCopy.FormatDateTime(target.CreatedAt) },
          button,
        },
      },
    };
  }
}

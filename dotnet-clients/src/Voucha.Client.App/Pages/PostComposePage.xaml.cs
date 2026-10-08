using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;

namespace Voucha.Client.App.Pages;

public partial class PostComposePage : ContentPage
{
  private CancellationTokenSource? imageSelectionBatchCancellationSource;
  private bool isPublishingInProgress;
  private readonly ITurnstileTokenProvider turnstileTokenProvider;
  private readonly PostComposeViewModel viewModel;
  private readonly EmailVerificationRecoveryCoordinator emailRecovery;

  public PostComposePage(
      PostComposeViewModel viewModel,
      ITurnstileTokenProvider turnstileTokenProvider,
      VouchaApiClient apiClient,
      EmailVerificationRecoveryCoordinator emailRecovery)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.turnstileTokenProvider = turnstileTokenProvider ?? throw new ArgumentNullException(nameof(turnstileTokenProvider));
    this.emailRecovery = emailRecovery ?? throw new ArgumentNullException(nameof(emailRecovery));
    MarkdownEditor.ApiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    BindingContext = viewModel;
  }

  protected override void OnDisappearing()
  {
    imageSelectionBatchCancellationSource?.Cancel();
    viewModel.EndLocalImagePreviewSession();
    base.OnDisappearing();
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not let publish failures escape.")]
  private async void OnPublishClicked(object? sender, EventArgs e)
  {
    if (isPublishingInProgress) return;
    isPublishingInProgress = true;
    try
    {
      if (!await EnsureTurnstileTokenAsync().ConfigureAwait(true))
      {
        return;
      }

      await viewModel.PublishAsync();
      await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
    finally
    {
      isPublishingInProgress = false;
    }
  }

  private void OnClearClicked(object? sender, EventArgs e)
  {
    imageSelectionBatchCancellationSource?.Cancel();
    viewModel.ResetDraft();
  }

  private void OnImageCaptionTextChanged(object? sender, TextChangedEventArgs e)
  {
    if (sender is not Entry entry || entry.BindingContext is not PostComposeImageDraft image) return;
    var caption = NormalizeCaption(e.NewTextValue);
    if (string.Equals(image.Caption, caption, StringComparison.Ordinal)) return;
    viewModel.SetImageCaption(image.ImageId, caption);
  }

  private void OnMoveImageUpClicked(object? sender, EventArgs e)
  {
    if (sender is not Button button || button.BindingContext is not PostComposeImageDraft image) return;
    viewModel.MoveImage(image.OrderIndex, image.OrderIndex - 1);
  }

  private void OnMoveImageDownClicked(object? sender, EventArgs e)
  {
    if (sender is not Button button || button.BindingContext is not PostComposeImageDraft image) return;
    viewModel.MoveImage(image.OrderIndex, image.OrderIndex + 1);
  }

  private void OnRemoveImageClicked(object? sender, EventArgs e)
  {
    if (sender is not Button button || button.BindingContext is not PostComposeImageDraft image) return;
    viewModel.RemoveImage(image.ImageId);
  }

  private static string? NormalizeCaption(string? text)
  {
    if (string.IsNullOrWhiteSpace(text)) return null;

    var caption = text.Trim();
    return caption.Length <= 1000 ? caption : caption[..1000];
  }

  private async Task<bool> EnsureTurnstileTokenAsync()
  {
    if (viewModel.CanUseCaptchaBypass || !string.IsNullOrWhiteSpace(viewModel.TurnstileToken))
    {
      return true;
    }

    var validation = viewModel.Validation;
    if (!viewModel.IsValidExceptCaptcha || !validation.RequiresCaptcha)
    {
      return true;
    }

    try
    {
      viewModel.TurnstileToken = await turnstileTokenProvider.GetTokenAsync().ConfigureAwait(true);
      return true;
    }
    catch (OperationCanceledException)
    {
      return false;
    }
    catch (InvalidOperationException ex)
    {
      viewModel.ReportTurnstileChallengeFailure(ex.Message);
      return false;
    }
  }
}

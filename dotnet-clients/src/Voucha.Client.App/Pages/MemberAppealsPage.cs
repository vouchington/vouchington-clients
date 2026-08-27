using System.Diagnostics;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

public sealed partial class MemberAppealsPage : ContentPage, IUiLocaleChangeListener, IDisposable
{
  private readonly MemberAppealsViewModel viewModel;
  private readonly ITurnstileTokenProvider turnstileTokenProvider;
  private readonly IDisposable? localeSubscription;
  private CancellationTokenSource lifetimeCancellation = new();
  private CancellationTokenSource? loadCancellation;
  private CancellationTokenSource? submissionCancellation;
  private Task loadSettlement = Task.CompletedTask;
  private Task? initialLoad;
  private bool isLoaded;
  private bool pageLoadFailed;
  private bool submissionOperationActive;

  public MemberAppealsPage(
      MemberAppealsViewModel viewModel,
      ITurnstileTokenProvider turnstileTokenProvider,
      IUiLocaleController? localeController = null)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.turnstileTokenProvider = turnstileTokenProvider ??
        throw new ArgumentNullException(nameof(turnstileTokenProvider));
    BindingContext = viewModel;
    SetDynamicResource(
        TitleProperty,
        UiMessageKey.NativeSwiftModerationAppealsMemberYourAppeals.Value);
    Content = BuildContent();
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public MemberAppealsRoute Route => viewModel.Route;

  public void OnUiLocaleChanged() => Content = BuildContent();

  public void Dispose() => localeSubscription?.Dispose();

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await RunPageOperationAsync(EnsureLoadedAsync).ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    CancelOperations();
    base.OnDisappearing();
  }

  private View BuildContent()
  {
    if (!viewModel.IsSignedIn) return SignInView();
    var content = new VerticalStackLayout { Padding = 16, Spacing = 12 };
    AddStatus(content);
    if (Route == MemberAppealsRoute.Tracking) AddTracking(content);
    AddNotices(content);
    if (viewModel.ActiveTarget is not null) content.Add(BuildForm());
    return new ScrollView { Content = content };
  }

  private void AddStatus(VerticalStackLayout content)
  {
    if (viewModel.IsLoading && viewModel.Appeals.Count == 0 &&
        viewModel.EligibleTargets.Count == 0)
    {
      content.Add(UiCopy.Bind(
          new Label { AutomationId = "member-appeals-loading" },
          Label.TextProperty,
          UiMessageKey.NativeSwiftModerationAppealsMemberLoading));
    }
    if (viewModel.HasLoadError || pageLoadFailed)
    {
      content.Add(UiCopy.Bind(
          new Label { AutomationId = "member-appeals-error", TextColor = Colors.IndianRed },
          Label.TextProperty,
          UiMessageKey.NativeSwiftModerationAppealsMemberGenericError));
      var retry = UiCopy.Bind(
          new Button { AutomationId = "member-appeals-retry" },
          Button.TextProperty,
          UiMessageKey.NativeCommonRetry);
      retry.Clicked += async (_, _) =>
          await RunPageOperationAsync(ReloadAsync).ConfigureAwait(true);
      content.Add(retry);
    }
    if (viewModel.SubmissionMessageKey is { } message)
    {
      content.Add(UiCopy.Bind(
          new Label { AutomationId = "member-appeal-submission-message" },
          Label.TextProperty,
          message));
    }
  }

  private void AddNotices(VerticalStackLayout content)
  {
    content.Add(Heading(
        UiMessageKey.NativeSwiftModerationAppealsMemberEligibleNotices,
        "member-appeals-notices-heading"));
    foreach (var target in viewModel.EligibleTargets) content.Add(TargetCard(target));
    if (viewModel.EligibleTargets.Count == 0 &&
        !viewModel.IsLoading &&
        !viewModel.HasLoadError)
    {
      content.Add(UiCopy.Bind(
          new Label { AutomationId = "member-appeals-empty" },
          Label.TextProperty,
          UiMessageKey.NativeSwiftModerationAppealsMemberNoEligibleNoticesMessage));
    }
    AddNoticePagination(content);
  }

  private static View SignInView() =>
      UiCopy.Bind(
          new Label { Margin = 16, AutomationId = "member-appeals-sign-in" },
          Label.TextProperty,
          UiMessageKey.NativeSwiftModerationAppealsMemberSignInMessage);

  private static Label Heading(UiMessageKey key, string id) =>
      UiCopy.Bind(
          new Label { FontAttributes = FontAttributes.Bold, FontSize = 20, AutomationId = id },
          Label.TextProperty,
          key);

  private async Task RunPageOperationAsync(Func<Task> operation)
  {
    try
    {
      await operation().ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
      Debug.WriteLine(ex);
      pageLoadFailed = true;
      Content = BuildContent();
    }
  }
}

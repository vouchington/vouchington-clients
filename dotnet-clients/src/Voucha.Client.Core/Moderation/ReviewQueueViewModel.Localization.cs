using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ReviewQueueViewModel
{
  public ReviewQueueViewModel(
      IModerationService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
      : this(
          service,
          exposureService: null,
          new AppConfig(new Uri(AppConfig.DefaultApiBaseUrl)),
          localization,
          localeController,
          utcNow: null,
          delay: null)
  {
  }

  public ReviewQueueViewModel(
      IModerationService service,
      IModerationExposureService exposureService,
      AppConfig appConfig,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
      : this(
          service,
          exposureService ?? throw new ArgumentNullException(nameof(exposureService)),
          appConfig,
          localization,
          localeController,
          utcNow: null,
          delay: null)
  {
  }

  internal ReviewQueueViewModel(
      IModerationService service,
      IModerationExposureService? exposureService,
      AppConfig appConfig,
      IUiLocalization? localization,
      IUiLocaleController? localeController,
      Func<DateTimeOffset>? utcNow,
      Func<TimeSpan, CancellationToken, Task>? delay)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.exposureService = exposureService;
    this.appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
    this.localization = localization ?? UiLocalization.English;
    this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    this.delay = delay ?? Task.Delay;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public void Dispose()
  {
    cooldownRefreshCancellation?.Cancel();
    cooldownRefreshCancellation?.Dispose();
    cooldownRefreshCancellation = null;
    localeSubscription?.Dispose();
  }

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(ErrorMessage));
    Items = Items.ToArray();
  }

  private void SetError(UiMessageKey? key)
  {
    if (errorMessageKey == key) return;
    errorMessageKey = key;
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(HasError));
  }
}

using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class EmailAddressManagerViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly IEmailAddressService service;
  private readonly IUiLocalization localization;
  private IReadOnlyList<EmailAddress> emailAddresses = [];
  private string emailAddress = string.Empty;
  private string otp = string.Empty;
  private string? errorMessage;
  private string? successMessage;
  private bool isAwaitingVerification;
  private bool isBusy;
  private readonly IDisposable? localeSubscription;

  public EmailAddressManagerViewModel(
      IEmailAddressService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public IReadOnlyList<EmailAddress> EmailAddresses
  {
    get => emailAddresses;
    private set
    {
      if (SetProperty(ref emailAddresses, value))
      {
        emailPages.ReplaceItems(value);
        OnPropertyChanged(nameof(EmptyStateMessage));
      }
    }
  }

  public string EmptyStateMessage => EmailAddresses.Count == 0
      ? localization.Localize(UiMessageKey.NativeDotnetCsharpNoEmailAddresses)
      : string.Empty;

  public string EmailAddress
  {
    get => emailAddress;
    set => SetProperty(ref emailAddress, value ?? string.Empty);
  }

  public string Otp
  {
    get => otp;
    set => SetProperty(ref otp, value ?? string.Empty);
  }

  public string? ErrorMessage
  {
    get => errorMessage;
    private set => SetProperty(ref errorMessage, value);
  }

  public string? SuccessMessage
  {
    get => successMessage;
    private set => SetProperty(ref successMessage, value);
  }

  public bool IsAwaitingVerification
  {
    get => isAwaitingVerification;
    private set => SetProperty(ref isAwaitingVerification, value);
  }

  public bool IsBusy
  {
    get => isBusy;
    private set
    {
      if (SetProperty(ref isBusy, value)) NotifyEmailPagination();
    }
  }

  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    emailPages.Reset(EmailAddresses);
    NotifyEmailPagination();
    await RunAsync(async () => ReplaceEmailPage(
        await service.FetchEmailAddressesPageAsync(null, 25, cancellationToken).ConfigureAwait(true))).ConfigureAwait(true);
  }

  public async Task<bool> RequestVerificationAsync(CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(EmailAddress))
    {
      ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpEnterEmail);
      return false;
    }

    return await RunAsync(async () =>
    {
      var response = await service.RequestEmailVerificationAsync(EmailAddress, cancellationToken).ConfigureAwait(true);
      EmailAddress = response.EmailAddress;
      IsAwaitingVerification = true;
    }).ConfigureAwait(true);
  }

  public async Task<bool> VerifyAsync(CancellationToken cancellationToken = default)
  {
    if (Otp.Trim().Length != 8)
    {
      ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpEnterVerificationCode);
      return false;
    }

    return await RunAsync(async () =>
    {
      var response = await service.VerifyEmailAddressAsync(EmailAddress, Otp.Trim(), cancellationToken).ConfigureAwait(true);
      ReplaceEmailPage(response);
      IsAwaitingVerification = false;
      SuccessMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpEmailVerified);
    }).ConfigureAwait(true);
  }

  private async Task<bool> RunAsync(Func<Task> operation)
  {
    IsBusy = true;
    ErrorMessage = null;
    SuccessMessage = null;
    try
    {
      await operation().ConfigureAwait(true);
      return true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex is VouchaApiException { ApiMessage: { Length: > 0 } message } ? message : ex.Message;
      return false;
    }
    finally
    {
      IsBusy = false;
    }
  }

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(EmptyStateMessage));
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(SuccessMessage));
  }

  public void Dispose() => localeSubscription?.Dispose();
}

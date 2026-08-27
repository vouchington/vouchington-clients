using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Support;

public sealed class StaffSupportContactViewModel(
    IStaffSupportService service,
    IUiLocalization? localization = null) : ObservableObject
{
  private readonly IUiLocalization localization = localization ?? UiLocalization.English;
  private SupportContact? contact;
  private IReadOnlyList<SupportThread> threads = [];
  private PageInfo? pageInfo;
  private bool isLoading;
  private string? errorMessage;
  public SupportContact? Contact { get => contact; private set => SetProperty(ref contact, value); }
  public IReadOnlyList<SupportThread> Threads { get => threads; private set => SetProperty(ref threads, value); }
  public bool HasMore => pageInfo?.HasNextPage == true;
  public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
  public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }

  public void ReportUnexpectedError(Exception exception)
  {
    ArgumentNullException.ThrowIfNull(exception);
    ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpError);
  }

  public Task LoadAsync(string contactId, CancellationToken cancellationToken = default) => LoadPageAsync(contactId, null, true, cancellationToken);
  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      Contact is not null && pageInfo?.EndCursor is { } cursor
          ? LoadPageAsync(Contact.Id, cursor, false, cancellationToken)
          : Task.CompletedTask;

  private async Task LoadPageAsync(string contactId, string? after, bool replace, CancellationToken cancellationToken)
  {
    if (IsLoading) return;
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      var response = await service.FetchContactAsync(contactId, after, 50, cancellationToken).ConfigureAwait(true);
      Contact = response.Contact;
      Threads = replace ? response.Threads : [.. Threads, .. response.Threads.Where(item => Threads.All(existing => existing.Id != item.Id))];
      pageInfo = response.ThreadPageInfo;
      OnPropertyChanged(nameof(HasMore));
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
    }
    finally { IsLoading = false; }
  }
}

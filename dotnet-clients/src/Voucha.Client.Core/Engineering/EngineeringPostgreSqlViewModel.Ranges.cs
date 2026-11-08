using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Engineering;

public sealed partial class EngineeringPostgreSqlViewModel
{
  private readonly ISessionStore? sessionStore;
  private CancellationTokenSource? rangeCancellation;
  private int sessionVersion;
  private string rangeFromDate = string.Empty;
  private string rangeToDate = string.Empty;
  private string rangeMaxDefaultRows = string.Empty;
  private PsqlPartitionRangeError rangeError;
  private bool rangeQueued;

  public string RangeFromDate { get => rangeFromDate; set => SetProperty(ref rangeFromDate, value ?? string.Empty); }
  public string RangeToDate { get => rangeToDate; set => SetProperty(ref rangeToDate, value ?? string.Empty); }
  public string RangeMaxDefaultRows { get => rangeMaxDefaultRows; set => SetProperty(ref rangeMaxDefaultRows, value ?? string.Empty); }
  public PsqlPartitionRangeError RangeError { get => rangeError; private set => SetProperty(ref rangeError, value); }
  public bool RangeQueued { get => rangeQueued; private set => SetProperty(ref rangeQueued, value); }
  public bool IsRangeAuthorized => sessionStore?.Current.Identity?.Roles?.Contains("administrator", StringComparer.Ordinal) == true;

  public async Task ConfirmRangeAsync(
      bool requestFamily,
      bool retire,
      Func<PsqlJobBody, Task<bool>> confirm,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(confirm);
    if (!IsRangeAuthorized || IsLoading || isMutating) return;
    RangeQueued = false;
    RangeError = PsqlPartitionRangeError.None;
    int? rows = null;
    if (!retire && !string.IsNullOrEmpty(RangeMaxDefaultRows))
    {
      if (!int.TryParse(RangeMaxDefaultRows, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedRows))
      {
        RangeError = PsqlPartitionRangeError.InvalidRowLimit;
        return;
      }
      rows = parsedRows;
    }
    var range = new PsqlClassifierRange(RangeFromDate, RangeToDate, rows);
    var body = new PsqlJobBody(retire ? "cleanupPartitions" : "createPartitions",
        ClassifierRange: requestFamily ? null : range, RequestRange: requestFamily ? range : null);
    RangeError = PsqlPartitionRange.Validate(body);
    if (RangeError != PsqlPartitionRangeError.None) return;
    var version = sessionVersion;
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    rangeCancellation = cancellation;
    await EngineeringRequest.RunAsync(
        () => IsLoading || isMutating,
        () => { isMutating = true; IsLoading = true; },
        message =>
        {
          if (version == sessionVersion && !cancellation.IsCancellationRequested) ErrorMessage = message;
        },
        () => { isMutating = false; IsLoading = false; },
        async () =>
        {
          if (!await confirm(body).ConfigureAwait(true)) return;
          cancellation.Token.ThrowIfCancellationRequested();
          if (version != sessionVersion || !IsRangeAuthorized) return;
          var result = await service.EnqueuePsqlJobAsync(body, cancellation.Token).ConfigureAwait(true);
          cancellation.Token.ThrowIfCancellationRequested();
          if (version != sessionVersion || !IsRangeAuthorized) return;
          RangeQueued = result.Success;
        }).ConfigureAwait(true);
    if (ReferenceEquals(rangeCancellation, cancellation)) rangeCancellation = null;
  }

  public void CancelRangeOperation() => rangeCancellation?.Cancel();

  private void OnRangeSessionChanged(object? sender, SessionChangedEventArgs e)
  {
    sessionVersion++;
    rangeCancellation?.Cancel();
    RangeQueued = false;
    RangeError = PsqlPartitionRangeError.None;
    OnPropertyChanged(nameof(IsRangeAuthorized));
  }
}

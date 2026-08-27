using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

public sealed partial class NativeOAuthAuthorizationCoordinator
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "OAuth callback failures must become observable state because platform app-link tasks are fire-and-forget.")]
  public async Task<NativeOAuthCallbackOutcome> HandleCallbackAsync(
      Uri uri,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(uri);
    if (!IsCallback(uri)) return new(false, null);
    var query = ParseQuery(uri.Query);
    if (!query.TryGetValue("flow_id", out var flowId) ||
        !query.TryGetValue("completion_token", out var token) ||
        string.IsNullOrWhiteSpace(flowId) ||
        string.IsNullOrWhiteSpace(token))
    {
      return new(false, null);
    }
    var purpose = Pending?.Purpose ?? Result?.Purpose;
    try
    {
      if (!await persistence.ClaimCallbackAsync(flowId, token, cancellationToken).ConfigureAwait(true))
      {
        await SynchronizeAsync(cancellationToken).ConfigureAwait(true);
        return new(true, Pending?.Purpose ?? Result?.Purpose ?? purpose);
      }
      await SynchronizeAsync(cancellationToken).ConfigureAwait(true);
      purpose = Pending?.Purpose ?? Result?.Purpose ?? purpose;
      if (Pending is not null)
      {
        await FinalizeAsync(Pending, cancellationToken).ConfigureAwait(true);
      }
      return new(true, purpose);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception ex)
    {
      RecordFailure(ex.Message);
      return new(true, purpose);
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Secure-storage failures must become observable state instead of escaping a MAUI page lifecycle.")]
  public async Task ResumeAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      await SynchronizeAsync(cancellationToken).ConfigureAwait(true);
      if (Pending is null) return;
      if (now() >= Pending.ExpiresAt)
      {
        await CompleteExpiredAsync(Pending, cancellationToken).ConfigureAwait(true);
        return;
      }
      if (Pending.CompletionToken is not null)
      {
        await FinalizeAsync(Pending, cancellationToken).ConfigureAwait(true);
        return;
      }
      State = NativeOAuthAuthorizationState.WaitingForCallback;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception ex)
    {
      RecordFailure(ex.Message);
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Secure-storage failures must remain retryable instead of escaping async-void MAUI cancel handlers.")]
  public async Task CancelAsync(CancellationToken cancellationToken = default)
  {
    if (State == NativeOAuthAuthorizationState.Finalizing) return;
    ErrorMessage = null;
    try
    {
      if (Pending is not null)
      {
        await persistence.ClearPendingAsync(cancellationToken).ConfigureAwait(true);
      }
      if (Result is not null)
      {
        await persistence.AcknowledgeResultAsync(cancellationToken).ConfigureAwait(true);
      }
      Pending = null;
      Result = null;
      State = NativeOAuthAuthorizationState.Cancelled;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception ex)
    {
      RecordFailure(ex.Message);
    }
  }

  private async Task SynchronizeAsync(CancellationToken cancellationToken)
  {
    var snapshot = await persistence.ReadAsync(cancellationToken).ConfigureAwait(true);
    Pending = snapshot.Pending;
    Result = snapshot.Result;
    if (Result is not null) State = StateFor(Result.Kind);
  }

  private static bool IsCallback(Uri uri) =>
      uri.Scheme.Equals("voucha", StringComparison.OrdinalIgnoreCase) &&
      uri.Host.Equals("auth", StringComparison.OrdinalIgnoreCase) &&
      uri.AbsolutePath == "/oauth/callback";

  private static Dictionary<string, string> ParseQuery(string query) =>
      query.TrimStart('?')
          .Split('&', StringSplitOptions.RemoveEmptyEntries)
          .Select(item => item.Split('=', 2))
          .Where(parts => parts.Length == 2)
          .GroupBy(parts => Uri.UnescapeDataString(parts[0]), StringComparer.Ordinal)
          .ToDictionary(
              group => group.Key,
              group => Uri.UnescapeDataString(group.First()[1]),
              StringComparer.Ordinal);

  private static NativeOAuthAuthorizationState StateFor(NativeOAuthAuthorizationResultKind kind) =>
      kind switch
      {
        NativeOAuthAuthorizationResultKind.Authenticated => NativeOAuthAuthorizationState.Authenticated,
        NativeOAuthAuthorizationResultKind.MfaRequired => NativeOAuthAuthorizationState.MfaRequired,
        NativeOAuthAuthorizationResultKind.Connected => NativeOAuthAuthorizationState.Connected,
        NativeOAuthAuthorizationResultKind.Expired => NativeOAuthAuthorizationState.Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
      };
}

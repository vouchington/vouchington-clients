using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

public sealed partial class MauiSecureSessionCookiePersistence : ISessionCookiePersistence
{
  private readonly ILogger<MauiSecureSessionCookiePersistence> logger;
  private readonly string storageKey;

  public MauiSecureSessionCookiePersistence(
      Uri apiBaseUrl,
      ILogger<MauiSecureSessionCookiePersistence> logger)
  {
    this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    storageKey = $"voucha.session-cookies.{HashOrigin(apiBaseUrl)}";
  }

  public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      return await SecureStorage.Default.GetAsync(storageKey).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      LogSessionReadFailed(logger, ex);
      return null;
    }
  }

  public async Task WriteAsync(string serializedCookies, CancellationToken cancellationToken = default)
  {
    try
    {
      await SecureStorage.Default.SetAsync(storageKey, serializedCookies).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      LogSessionWriteFailed(logger, ex);
    }
  }

  public Task ClearAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      SecureStorage.Default.Remove(storageKey);
    }
    catch (Exception ex)
    {
      LogSessionClearFailed(logger, ex);
      throw;
    }

    return Task.CompletedTask;
  }

  private static string HashOrigin(Uri apiBaseUrl)
  {
    var origin = apiBaseUrl.IsDefaultPort
        ? $"{apiBaseUrl.Scheme}://{apiBaseUrl.Host}"
        : $"{apiBaseUrl.Scheme}://{apiBaseUrl.Host}:{apiBaseUrl.Port}";
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(origin)));
  }

  [LoggerMessage(
      EventId = 1,
      Level = LogLevel.Warning,
      Message = "SecureStorage session read failed.")]
  private static partial void LogSessionReadFailed(
      ILogger logger,
      Exception exception);

  [LoggerMessage(
      EventId = 2,
      Level = LogLevel.Warning,
      Message = "SecureStorage session write failed.")]
  private static partial void LogSessionWriteFailed(
      ILogger logger,
      Exception exception);

  [LoggerMessage(
      EventId = 3,
      Level = LogLevel.Error,
      Message = "SecureStorage session clear failed.")]
  private static partial void LogSessionClearFailed(
      ILogger logger,
      Exception exception);
}

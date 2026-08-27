namespace Voucha.Client.Core.Auth;

public interface ISessionCookiePersistence
{
  Task<string?> ReadAsync(CancellationToken cancellationToken = default);

  Task WriteAsync(string serializedCookies, CancellationToken cancellationToken = default);

  Task ClearAsync(CancellationToken cancellationToken = default);
}

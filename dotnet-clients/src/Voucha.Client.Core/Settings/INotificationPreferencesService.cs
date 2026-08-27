using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public interface INotificationPreferencesService
{
  Task<EmailPreferencesResponse> FetchEmailPreferencesAsync(
      CancellationToken cancellationToken = default);

  Task<EmailPreferencesResponse> UpdateEmailPreferencesAsync(
      UpdateEmailPreferencesBody body,
      CancellationToken cancellationToken = default);
}

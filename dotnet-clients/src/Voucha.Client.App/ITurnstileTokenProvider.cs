namespace Voucha.Client.App;

public interface ITurnstileTokenProvider
{
  Task<string> GetTokenAsync(string routeAction, CancellationToken cancellationToken = default);
}

namespace Voucha.Client.App;

public interface ITurnstileTokenProvider
{
  Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}

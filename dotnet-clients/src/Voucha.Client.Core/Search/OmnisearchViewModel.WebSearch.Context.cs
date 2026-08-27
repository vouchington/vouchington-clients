namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  private string? TrimmedQueryOrNull() =>
      string.IsNullOrWhiteSpace(Query) ? null : Query.Trim();

  private string CurrentUserIdOrUsername() =>
      sessionStore?.Current.Identity?.Username ??
      sessionStore?.Current.Identity?.Id ??
      throw new InvalidOperationException("Current user identity is required for bookmarked web search routes.");
}

namespace Voucha.Client.Core;

public enum SentryAccountAction
{
  Unchanged,
  Set,
  Clear,
}

public readonly record struct SentryAccountDecision(SentryAccountAction Action, string? AccountId)
{
  public static SentryAccountDecision Update(string? boundAccountId, string? nextAccountId)
  {
    var current = SentryTrimmedText.NonEmpty(boundAccountId);
    var next = SentryTrimmedText.NonEmpty(nextAccountId);
    if (string.Equals(current, next, StringComparison.Ordinal))
    {
      return new SentryAccountDecision(SentryAccountAction.Unchanged, current);
    }

    return next is null
        ? new SentryAccountDecision(SentryAccountAction.Clear, null)
        : new SentryAccountDecision(SentryAccountAction.Set, next);
  }
}

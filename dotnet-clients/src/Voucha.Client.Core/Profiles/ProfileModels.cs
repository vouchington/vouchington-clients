namespace Voucha.Client.Core.Profiles;

public enum ProfileHistoryTab
{
  All,
  Reviews,
  Discussions,
  Comments,
}

public sealed record ProfileHistoryTabRow(
    ProfileHistoryTab Tab,
    string Label,
    int Count,
    bool IsSelected);

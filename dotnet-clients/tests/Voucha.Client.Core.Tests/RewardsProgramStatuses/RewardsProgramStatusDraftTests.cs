using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.RewardsProgramStatuses;
using Xunit;

namespace Voucha.Client.Core.Tests.RewardsProgramStatuses;

public sealed class RewardsProgramStatusDraftTests
{
  [Fact]
  public void TryBuildUpdateEncodesIsoDatesAndExplicitClears()
  {
    var original = Status(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31));
    var draft = new RewardsProgramStatusDraft(original) { Since = "2024-02-01", Until = string.Empty };

    Assert.True(draft.TryBuildUpdate(out var body));
    Assert.NotNull(body);
    Assert.Equal(new DateOnly(2024, 2, 1), body.Since!.Value.Value);
    Assert.Null(body.Until!.Value.Value);
  }

  [Fact]
  public void TryBuildUpdateSkipsNoOpAndRejectsNonIsoDate()
  {
    var draft = new RewardsProgramStatusDraft(Status(new DateOnly(2024, 1, 1), null));
    Assert.True(draft.TryBuildUpdate(out var noOp));
    Assert.Null(noOp);
    draft.Since = "01/02/2024";
    Assert.False(draft.TryBuildUpdate(out _));
  }

  [Fact]
  public void TryBuildUpdateRejectsReversedDateRange()
  {
    var draft = new RewardsProgramStatusDraft(Status(null, null))
    {
      SinceEnabled = true,
      Since = "2024-02-01",
      UntilEnabled = true,
      Until = "2024-01-01",
    };

    Assert.False(draft.TryBuildUpdate(out _));
  }

  [Fact]
  public void EnablingAnAbsentDateInitializesItsBackingValue()
  {
    var draft = new RewardsProgramStatusDraft(Status(null, null));
    draft.SinceEnabled = true;
    draft.UntilEnabled = true;

    Assert.True(DateOnly.TryParseExact(draft.Since, "yyyy-MM-dd", out _));
    Assert.True(DateOnly.TryParseExact(draft.Until, "yyyy-MM-dd", out _));
  }

  [Fact]
  public void IsoDatesRemainGregorianUnderANonGregorianCurrentCulture()
  {
    var previousCulture = CultureInfo.CurrentCulture;
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
    try
    {
      var draft = new RewardsProgramStatusDraft(Status(new DateOnly(2024, 1, 1), null)) { Since = "2024-02-01" };

      Assert.Equal(new DateTime(2024, 2, 1), draft.SinceDate);
      Assert.True(draft.TryBuildUpdate(out var body));
      Assert.Equal(new DateOnly(2024, 2, 1), body!.Since!.Value.Value);
    }
    finally
    {
      CultureInfo.CurrentCulture = previousCulture;
    }
  }

  [Fact]
  public void EndpointsPreserveCursorAndTopicType()
  {
    var page = VouchaApiEndpoints.RewardsProgramStatuses("opaque", 2);
    var search = VouchaApiEndpoints.RewardsProgramStatusTopics("Gold");
    Assert.Equal("opaque", page.Query["after"]);
    Assert.Equal("2", page.Query["limit"]);
    Assert.Equal("rewards_program_status", search.Query["topic_types"]);
  }

  private static RewardsProgramStatus Status(DateOnly? since, DateOnly? until) => new(
      "row", "topic", new RewardsProgramSummary("program", "Program", "program"), since, until);
}

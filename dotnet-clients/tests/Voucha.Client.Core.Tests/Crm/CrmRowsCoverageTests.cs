using Voucha.Client.Core.Api;
using Voucha.Client.Core.Crm;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Crm;

public sealed class CrmRowsCoverageTests
{
  [Theory]
  [InlineData(nameof(CrmContact.OptedOutAt), "Opted out")]
  [InlineData(nameof(CrmContact.ArchivedAt), "Archived")]
  [InlineData(nameof(CrmContact.ConvertedAt), "Converted")]
  [InlineData(nameof(CrmContact.RespondedAt), "In conversation")]
  [InlineData(nameof(CrmContact.ContactedAt), "Awaiting response")]
  [InlineData("", "New")]
  public void ContactRowsMapStatusPriority(string timestampName, string expectedStatus)
  {
    var row = CrmContactRow.FromContact(Contact(timestampName));
    var expectedDate = UiLocalization.English.FormatDateTime(
        new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
        TimeZoneInfo.Local);

    Assert.Equal(expectedStatus, row.LocalizedStatus);
    Assert.Equal(timestampName == "" ? null : expectedDate, row.ContactedAt);
    Assert.Null(row.UserId);
  }

  [Theory]
  [InlineData(CrmContactVertical.CreditCards, "credit_cards")]
  [InlineData(CrmContactVertical.Travel, "travel")]
  [InlineData(CrmContactVertical.Cars, "cars")]
  [InlineData(CrmContactVertical.Ai, "ai")]
  [InlineData(CrmContactVertical.Technology, "technology")]
  [InlineData(CrmContactVertical.Finance, "finance")]
  [InlineData(CrmContactVertical.Lifestyle, "lifestyle")]
  [InlineData(CrmContactVertical.Other, "other")]
  [InlineData((CrmContactVertical)99, "99")]
  public void ContactRowsMapVerticals(CrmContactVertical vertical, string expectedVertical)
  {
    var row = CrmContactRow.FromContact(Contact(vertical: vertical, userId: "user-1"));

    Assert.Equal(expectedVertical, row.ProtocolVertical);
    Assert.Equal("user-1", row.UserId);
  }

  [Fact]
  public void EmailRowsMapFallbacksAndUnknownDirection()
  {
    var row = CrmEmailRow.FromMessage(new CrmMessage(
        "message-1",
        "conversation-1",
        (CrmMessageDirection)99,
        "alice@example.test",
        "team@example.test",
        null,
        null,
        "<p>Hi</p>",
        CrmEmailProvider.Ses,
        null,
        new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero)));

    Assert.Equal("(no subject)", row.Subject);
    Assert.Equal("Unknown direction", row.LocalizedDirection);
    Assert.Equal(
        UiLocalization.English.FormatDateTime(
            new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
            TimeZoneInfo.Local),
        row.SentAt);
  }

  private static CrmContact Contact(
      string timestampName = "",
      CrmContactVertical? vertical = CrmContactVertical.CreditCards,
      string? userId = null)
  {
    var timestamp = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
    return new CrmContact(
        "contact-1",
        "Alice Creator",
        "alice@example.test",
        null,
        vertical,
        CrmContactType.Influencer,
        CrmContactSource.Manual,
        250000,
        null,
        null,
        userId,
        null,
        "created-by",
        timestampName == "" ? null : timestamp,
        timestampName == nameof(CrmContact.RespondedAt) ? timestamp : null,
        timestampName == nameof(CrmContact.ConvertedAt) ? timestamp : null,
        timestampName == nameof(CrmContact.OptedOutAt) ? timestamp : null,
        timestampName == nameof(CrmContact.ArchivedAt) ? timestamp : null,
        timestamp,
        timestamp);
  }
}
